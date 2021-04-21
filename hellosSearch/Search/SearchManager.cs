using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Zenject;
using HUI.Interfaces;
using HUI.Utilities;
using HUISearch.UI.Screens;
using static HUISearch.Search.WordPredictionEngine;

namespace HUISearch.Search
{
    public class SearchManager : IInitializable, IDisposable, ILevelCollectionModifier
    {
        public event Action<bool> LevelCollectionRefreshRequested;

        public int Priority => 100;

        private WordSearchEngine _wordSearchEngine;
        private WordPredictionEngine _wordPredictionEngine;
        private SearchScreenManager _searchScreenManager;
        private SearchKeyboardScreenManager _searchKeyboardScreenManager;

        private IAnnotatedBeatmapLevelCollection _originalLevelCollection;

        private StringBuilder _searchText;

        [Inject]
        public SearchManager(
            WordSearchEngine wordSearchEngine,
            WordPredictionEngine wordPredictionEngine,
            SearchScreenManager searchScreenManager,
            SearchKeyboardScreenManager searchKeyboardScreenManager)
        {
            _wordSearchEngine = wordSearchEngine;
            _wordPredictionEngine = wordPredictionEngine;
            _searchScreenManager = searchScreenManager;
            _searchKeyboardScreenManager = searchKeyboardScreenManager;

            _searchText = new StringBuilder();
        }

        public void Initialize()
        {
            PluginConfig.Instance.ConfigReloaded += OnSearchOptionChanged;

            _searchScreenManager.SearchButtonPressed += OnSearchScreenSearchButtonPressed;
            _searchScreenManager.CancelButtonPressed += OnSearchScreenCancelButtonPressed;

            _searchKeyboardScreenManager.KeyPressed += OnSearchKeyboardScreenKeyPressed;
            _searchKeyboardScreenManager.DeleteButtonPressed += OnSearchKeyboardScreenDeleteButtonPressed;
            _searchKeyboardScreenManager.ClearButtonPressed += ClearSearchText;
            _searchKeyboardScreenManager.PredictionButtonPressed += OnSearchKeyboardScreenPredictionButtonPressed;
            _searchKeyboardScreenManager.SearchOptionChanged += OnSearchOptionChanged;
        }

        public void Dispose()
        {
            if (PluginConfig.Instance != null)
                PluginConfig.Instance.ConfigReloaded -= OnSearchOptionChanged;

            if (_searchScreenManager != null)
            {
                _searchScreenManager.SearchButtonPressed -= OnSearchScreenSearchButtonPressed;
                _searchScreenManager.CancelButtonPressed -= OnSearchScreenCancelButtonPressed;
            }

            if (_searchKeyboardScreenManager != null)
            {
                _searchKeyboardScreenManager.KeyPressed -= OnSearchKeyboardScreenKeyPressed;
                _searchKeyboardScreenManager.DeleteButtonPressed -= OnSearchKeyboardScreenDeleteButtonPressed;
                _searchKeyboardScreenManager.ClearButtonPressed -= ClearSearchText;
                _searchKeyboardScreenManager.PredictionButtonPressed -= OnSearchKeyboardScreenPredictionButtonPressed;
                _searchKeyboardScreenManager.SearchOptionChanged -= OnSearchOptionChanged;
            }
        }

        public void OnLevelCollectionSelected(IAnnotatedBeatmapLevelCollection annotatedBeatmaplevelCollection)
        {
            _originalLevelCollection = annotatedBeatmaplevelCollection;
            _wordPredictionEngine.SetActiveWordStorageFromLevelCollection(annotatedBeatmaplevelCollection);

            // search keyboard will handle clearing the query (and running associated callbacks)
            // and hiding itself if necessary
            _searchKeyboardScreenManager.OnLevelCollectionSelected();

            // view should already be accurate and level collection refresh should already be requested
            if (!PluginConfig.Instance.ClearQueryOnSelectLevelCollection)
                UpdateSearchResults();
        }

        public bool ApplyModifications(IEnumerable<IPreviewBeatmapLevel> levelCollection, out IEnumerable<IPreviewBeatmapLevel> modifiedLevelCollection)
        {
            if (_searchText.Length == 0)
            {
                modifiedLevelCollection = levelCollection;
                return false;
            }
            else if (_wordSearchEngine.SearchFinished)
            {
                modifiedLevelCollection = levelCollection.Where(x => _wordSearchEngine.CachedResult.Contains(x));
                return true;
            }
            else
            {
                // search results are not ready yet
                // show an empty list first and show the results later
                modifiedLevelCollection = Array.Empty<IPreviewBeatmapLevel>();
                return true;
            }
        }

        private void UpdateView()
        {
            string searchText = _searchText.ToString();

            _searchScreenManager.SearchText = searchText;
            _searchKeyboardScreenManager.SearchText = searchText;
            _searchKeyboardScreenManager.SetSuggestedWords(_wordPredictionEngine.GetSuggestedWords(searchText));

            // note: UpdateView is only called when the query has changed,
            // so we clear the results number text and set it after getting back the search results
            _searchKeyboardScreenManager.SetResultsNumber(-1);
        }

        private void UpdateSearchResults(bool startNewSearch = true)
        {
            if (_searchText.Length == 0)
                return;
            else if (startNewSearch)
                _wordSearchEngine.StartNewSearch(_originalLevelCollection.beatmapLevelCollection.beatmapLevels, _searchText.ToString(), OnSearchFinished);
            else
                _wordSearchEngine.StartSearchOnExistingList(_searchText.ToString(), OnSearchFinished);
        }

        private void OnSearchFinished(IEnumerable<IPreviewBeatmapLevel> levels)
        {
            _searchKeyboardScreenManager.SetResultsNumber(levels.Count());

            RequestLevelCollectionRefresh();
        }

        private void ClearSearchText()
        {
            if (_searchText.Length > 0)
            {
                _searchText.Clear();

                UpdateSearchResults();
                UpdateView();
                RequestLevelCollectionRefresh();
            }
        }

        private void OnSearchOptionChanged()
        {
            UpdateSearchResults();
            RequestLevelCollectionRefresh();
        }

        private void OnSearchScreenSearchButtonPressed()
        {
            if (_searchKeyboardScreenManager.IsVisible)
                _searchKeyboardScreenManager.HideScreen();
            else
                _searchKeyboardScreenManager.ShowScreen();
        }

        private void OnSearchScreenCancelButtonPressed()
        {
            if (_searchKeyboardScreenManager.IsVisible)
                _searchKeyboardScreenManager.HideScreen();

            ClearSearchText();
        }

        private void OnSearchKeyboardScreenKeyPressed(char key)
        {
            _searchText.Append(key);

            UpdateSearchResults(_searchText.Length <= 1);
            UpdateView();
            RequestLevelCollectionRefresh();
        }

        private void OnSearchKeyboardScreenDeleteButtonPressed()
        {
            if (_searchText.Length > 0)
                _searchText.Remove(_searchText.Length - 1, 1);

            UpdateSearchResults();
            UpdateView();
            RequestLevelCollectionRefresh();
        }

        private void OnSearchKeyboardScreenPredictionButtonPressed(SuggestedWord suggestedWord)
        {
            if (_searchText.Length == 0)
            {
                _searchText.Append(suggestedWord.Word);
            }
            else
            {
                string searchText = _searchText.ToString();
                string[] searchTextWords = StringUtilities.RemoveSymbolsRegex.Replace(searchText, " ").Split(StringUtilities.SpaceCharArray);

                if (suggestedWord.Type == SuggestionType.Prefixed || suggestedWord.Type == SuggestionType.FuzzyMatch)
                {
                    int index = searchText.LastIndexOf(searchTextWords[searchTextWords.Length - 1]);
                    _searchText.Remove(index, _searchText.Length - index);
                }
                else if (suggestedWord.Type == SuggestionType.FollowUp)
                {
                    _searchText.Append(searchText[searchText.Length - 1] == ' ' ? "" : " ");
                }
                else
                {
                    _searchText.Clear();
                }

                _searchText.Append(suggestedWord.Word);
            }

            UpdateSearchResults(false);
            UpdateView();
            RequestLevelCollectionRefresh();
        }

        private void RequestLevelCollectionRefresh(bool selectLastLevel = false) => this.CallAndHandleAction(LevelCollectionRefreshRequested, nameof(LevelCollectionRefreshRequested), selectLastLevel);
    }
}
