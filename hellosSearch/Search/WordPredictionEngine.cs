using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using Zenject;
using SongCore;
using HUI.Utilities;

namespace HUISearch.Search
{
    public class WordPredictionEngine : IInitializable, IDisposable
    {
        private BeatmapLevelsModel _beatmapLevelsModel;

        private LevelCollectionWordStorage _activeWordStorage = null;
        private Dictionary<string, LevelCollectionWordStorage> _cache = new Dictionary<string, LevelCollectionWordStorage>();
        private bool _wasDeletingSong = false;

        public const int SuggestedWordsCountThreshold = 10;

        private const string AllLevelsKey = "HUIAllBeatmaps";

        [Inject]
        public WordPredictionEngine(BeatmapLevelsModel beatmapLevelsModel)
        {
            _beatmapLevelsModel = beatmapLevelsModel;
        }

        public void Initialize()
        {
            Loader.LoadingStartedEvent += OnSongCoreLoadingStarted;
            Loader.SongsLoadedEvent += OnSongCoreSongsLoaded;
            Loader.OnLevelPacksRefreshed += OnSongCoreLevelPacksRefreshed;
            Loader.DeletingSong += OnSongCoreDeletingSong;
        }

        public void Dispose()
        {
            Loader.LoadingStartedEvent -= OnSongCoreLoadingStarted;
            Loader.SongsLoadedEvent -= OnSongCoreSongsLoaded;
            Loader.OnLevelPacksRefreshed -= OnSongCoreLevelPacksRefreshed;
            Loader.DeletingSong -= OnSongCoreDeletingSong;
        }

        private void OnSongCoreLoadingStarted(Loader loader)
        {
            CancelTasks();
        }

        private void OnSongCoreSongsLoaded(Loader loader, ConcurrentDictionary<string, CustomPreviewBeatmapLevel> customLevels)
        {
            CancelTasks();
            ClearCache();
        }

        private void OnSongCoreLevelPacksRefreshed()
        {
            // deleting a song will automatically select the Custom Levels pack after the deletion,
            // so we shouldn't clear the cache in that situation
            if (_wasDeletingSong)
            {
                _wasDeletingSong = false;
                return;
            }

            CancelTasks();
            ClearCache();
        }

        private void OnSongCoreDeletingSong()
        {
            CancelTasks();
            ClearCache();

            _wasDeletingSong = true;
        }

        public void SetActiveWordStorageFromLevelCollection(IAnnotatedBeatmapLevelCollection levelCollection)
        {
            string key;
            if (levelCollection is IBeatmapLevelPack levelPack)
                key = levelPack.packID;
            else
                key = levelCollection.collectionName;

            LevelCollectionWordStorage storage = null;
            if (string.IsNullOrEmpty(key))
            {
                // when the key is null, assume we're looking at all level packs
                if (!_cache.TryGetValue(AllLevelsKey, out storage))
                {
                    storage = new LevelCollectionWordStorage(_beatmapLevelsModel.allLoadedBeatmapLevelPackCollection.beatmapLevelPacks.SelectMany(x => x.beatmapLevelCollection.beatmapLevels));
                    _cache[AllLevelsKey] = storage;
                }

            }
            else if (!_cache.TryGetValue(key, out storage))
            {
                storage = new LevelCollectionWordStorage(levelCollection);
                _cache[key] = storage;
            }

            _activeWordStorage = storage;
        }

        /// <summary>
        /// Pause ongoing WordCountStorage creation tasks. This should be used when the player is playing 
        /// the game to reduce whatever performance penalty these background tasks could have.
        /// </summary>
        public void PauseTasks()
        {
            foreach (var storage in _cache.Values)
                storage.PauseSetup();
        }

        /// <summary>
        /// Resume tasks that were paused with PauseTasks().
        /// </summary>
        public void ResumeTasks()
        {
            foreach (var storage in _cache.Values)
                storage.ResumeSetup();
        }

        private void CancelTasks()
        {
            foreach (var storage in _cache.Values)
                storage.CancelSetup();
        }

        private void ClearCache()
        {
            Plugin.Log.Info("Clearing word prediction storage cache");

            _cache.Clear();
            _activeWordStorage = null;
        }

        /// <summary>
        /// Gets a list of suggested words from the active word storage, based on what was typed by the user.
        /// </summary>
        /// <param name="searchQuery">The typed search query.</param>
        /// <returns>A list of suggested words.</returns>
        public IEnumerable<SuggestedWord> GetSuggestedWords(string searchQuery)
        {
            if (string.IsNullOrEmpty(searchQuery) || _activeWordStorage == null || !_activeWordStorage.IsReady)
                return Array.Empty<SuggestedWord>();

            string[] words = searchQuery.Split(StringUtilities.SpaceCharArray, StringSplitOptions.RemoveEmptyEntries);

            // query only had spaces
            if (words.Length == 0)
                return Array.Empty<SuggestedWord>();

            // note to self: don't use IEnumerable here, since Concat can cause a stack overflow by building an unbalanced tree
            List<SuggestedWord> suggestedWords = new List<SuggestedWord>();
            string lastWord = words[words.Length - 1];

            if (searchQuery[searchQuery.Length - 1] != ' ')
            {
                suggestedWords.AddRange(
                    _activeWordStorage.GetWordsWithPrefix(lastWord)
                        .Select(word => new SuggestedWord(word, SuggestionType.Prefixed)));
            }

            if (suggestedWords.Count >= SuggestedWordsCountThreshold)
                return suggestedWords;

            suggestedWords.AddRange(
                _activeWordStorage.GetFollowUpWords(lastWord)
                    .Where(word => !suggestedWords.Any(suggested => suggested.Word == word))
                    .Select(word => new SuggestedWord(word, SuggestionType.FollowUp)));

            // if we still don't reach the threshold of suggestions, assume there's a/some typo(s) => use fuzzy string matching
            if (suggestedWords.Count >= SuggestedWordsCountThreshold)
                return suggestedWords;

            int tolerance = Math.Min(2, Convert.ToInt32(lastWord.Length * 0.7f));
            suggestedWords.AddRange(
                _activeWordStorage.GetFuzzyMatchedWords(lastWord, tolerance)
                    .Where(word => !suggestedWords.Any(suggested => suggested.Word == word))
                    .Select(word => new SuggestedWord(word, SuggestionType.FuzzyMatch)));

            if (suggestedWords.Count >= SuggestedWordsCountThreshold)
                return suggestedWords;

            suggestedWords.AddRange(
                _activeWordStorage.GetFuzzyMatchedWordsAlternate(lastWord)
                    .Where(word => !suggestedWords.Any(suggested => suggested.Word == word))
                    .Select(word => new SuggestedWord(word, SuggestionType.FuzzyMatch)));

            return suggestedWords;
        }

        public struct SuggestedWord
        {
            public string Word;
            public SuggestionType Type;

            public SuggestedWord(string word, SuggestionType type)
            {
                Word = word;
                Type = type;
            }
        }

        public enum SuggestionType
        {
            Prefixed,
            FollowUp,
            FuzzyMatch
        }
    }
}
