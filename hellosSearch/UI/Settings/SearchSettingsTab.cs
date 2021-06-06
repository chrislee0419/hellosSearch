using System;
using HMUI;
using BeatSaberMarkupLanguage.Attributes;
using HUI.Interfaces;
using HUI.Utilities;
using HUISearch.Search;

namespace HUISearch.UI.Settings
{
    public class SearchSettingsTab : SettingsModalTabBase
    {
        public event Action OffHandLaserPointerSettingChanged;
        public event Action SearchOptionChanged;
        public event Action<bool> AllowScreenMovementClicked;

        public override string TabName => "Search";
        protected override string AssociatedBSMLResource => "HUISearch.UI.Views.SearchSettingsView.bsml";

        [UIValue("close-screen-on-select-level-value")]
        public bool CloseScreenOnSelectLevelValue
        {
            get => PluginConfig.Instance.CloseScreenOnSelectLevel;
            set
            {
                if (PluginConfig.Instance.CloseScreenOnSelectLevel == value)
                    return;

                PluginConfig.Instance.CloseScreenOnSelectLevel = value;
            }
        }

        [UIValue("close-screen-on-select-level-collection-value")]
        public bool CloseScreenOnLevelCollectionSelectValue
        {
            get => PluginConfig.Instance.CloseScreenOnSelectLevelCollection;
            set
            {
                if (PluginConfig.Instance.CloseScreenOnSelectLevelCollection == value)
                    return;

                PluginConfig.Instance.CloseScreenOnSelectLevelCollection = value;
            }
        }

        [UIValue("clear-query-on-select-level-collection-value")]
        public bool ClearQueryOnSelectLevelCollection
        {
            get => PluginConfig.Instance.ClearQueryOnSelectLevelCollection;
            set
            {
                if (PluginConfig.Instance.ClearQueryOnSelectLevelCollection == value)
                    return;

                PluginConfig.Instance.ClearQueryOnSelectLevelCollection = value;
            }
        }

        [UIValue("off-hand-laser-pointer-value")]
        public bool OffHandLaserPointer
        {
            get => PluginConfig.Instance.UseOffHandLaserPointer;
            set
            {
                if (PluginConfig.Instance.UseOffHandLaserPointer == value)
                    return;

                PluginConfig.Instance.UseOffHandLaserPointer = value;

                this.CallAndHandleAction(OffHandLaserPointerSettingChanged, nameof(OffHandLaserPointerSettingChanged));
            }
        }

        [UIValue("strip-symbols-value")]
        public bool StripSymbols
        {
            get => PluginConfig.Instance.StripSymbols;
            set
            {
                if (PluginConfig.Instance.StripSymbols == value)
                    return;

                PluginConfig.Instance.StripSymbols = value;
                this.CallAndHandleAction(SearchOptionChanged, nameof(SearchOptionChanged));
            }
        }

        [UIValue("split-query-value")]
        public bool SplitQueryByWords
        {
            get => PluginConfig.Instance.SplitQueryByWords;
            set
            {
                if (PluginConfig.Instance.SplitQueryByWords == value)
                    return;

                PluginConfig.Instance.SplitQueryByWords = value;
                this.CallAndHandleAction(SearchOptionChanged, nameof(SearchOptionChanged));
            }
        }

        [UIValue("song-fields-song-title-value")]
        public bool SearchSongTitleFieldValue
        {
            get => (PluginConfig.Instance.SongFieldsToSearch | WordSearchEngine.SearchableSongFields.SongName) != 0;
            set
            {
                WordSearchEngine.SearchableSongFields expectedValue;
                if (value)
                    expectedValue = PluginConfig.Instance.SongFieldsToSearch | WordSearchEngine.SearchableSongFields.SongName;
                else
                    expectedValue = PluginConfig.Instance.SongFieldsToSearch & ~WordSearchEngine.SearchableSongFields.SongName;

                if (PluginConfig.Instance.SongFieldsToSearch == expectedValue)
                    return;

                PluginConfig.Instance.SongFieldsToSearch = expectedValue;
                this.CallAndHandleAction(SearchOptionChanged, nameof(SearchOptionChanged));
            }
        }

        [UIValue("song-fields-song-author-value")]
        public bool SearchSongAuthorFieldValue
        {
            get => (PluginConfig.Instance.SongFieldsToSearch | WordSearchEngine.SearchableSongFields.SongAuthor) != 0;
            set
            {
                WordSearchEngine.SearchableSongFields expectedValue;
                if (value)
                    expectedValue = PluginConfig.Instance.SongFieldsToSearch | WordSearchEngine.SearchableSongFields.SongAuthor;
                else
                    expectedValue = PluginConfig.Instance.SongFieldsToSearch & ~WordSearchEngine.SearchableSongFields.SongAuthor;

                if (PluginConfig.Instance.SongFieldsToSearch == expectedValue)
                    return;

                PluginConfig.Instance.SongFieldsToSearch = expectedValue;
                this.CallAndHandleAction(SearchOptionChanged, nameof(SearchOptionChanged));
            }
        }

        [UIValue("song-fields-level-author-value")]
        public bool SearchLevelAuthorFieldValue
        {
            get => (PluginConfig.Instance.SongFieldsToSearch | WordSearchEngine.SearchableSongFields.LevelAuthor) != 0;
            set
            {
                WordSearchEngine.SearchableSongFields expectedValue;
                if (value)
                    expectedValue = PluginConfig.Instance.SongFieldsToSearch | WordSearchEngine.SearchableSongFields.LevelAuthor;
                else
                    expectedValue = PluginConfig.Instance.SongFieldsToSearch & ~WordSearchEngine.SearchableSongFields.LevelAuthor;

                if (PluginConfig.Instance.SongFieldsToSearch == expectedValue)
                    return;

                PluginConfig.Instance.SongFieldsToSearch = expectedValue;
                this.CallAndHandleAction(SearchOptionChanged, nameof(SearchOptionChanged));
            }
        }

        [UIValue("song-fields-contributors-value")]
        public bool SearchContributorsFieldValue
        {
            get => (PluginConfig.Instance.SongFieldsToSearch | WordSearchEngine.SearchableSongFields.Contributors) != 0;
            set
            {
                WordSearchEngine.SearchableSongFields expectedValue;
                if (value)
                    expectedValue = PluginConfig.Instance.SongFieldsToSearch | WordSearchEngine.SearchableSongFields.Contributors;
                else
                    expectedValue = PluginConfig.Instance.SongFieldsToSearch & ~WordSearchEngine.SearchableSongFields.Contributors;

                if (PluginConfig.Instance.SongFieldsToSearch == expectedValue)
                    return;

                PluginConfig.Instance.SongFieldsToSearch = expectedValue;
                this.CallAndHandleAction(SearchOptionChanged, nameof(SearchOptionChanged));
            }
        }

        [UIValue("allow-screen-movement-button-text")]
        public string AllowScreenMovementButtonText => _allowScreenMovement ? "Disable Repositioning" : "Enable Repositioning";

        [UIValue("set-default-screen-position-button-text")]
        public string SetDefaultScreenPositionButtonText => _allowScreenMovement ? "Undo Changes" : "Reset to Default";

        private bool _allowScreenMovement = false;
        public bool AllowScreenMovement
        {
            get => _allowScreenMovement;
            set
            {
                if (_allowScreenMovement == value)
                    return;

                _allowScreenMovement = value;
                NotifyPropertyChanged(nameof(AllowScreenMovementButtonText));
                NotifyPropertyChanged(nameof(SetDefaultScreenPositionButtonText));
            }
        }

        public override void OnTabHidden()
        {
            AllowScreenMovement = false;
        }

        [UIAction("tab-selected")]
        private void OnTabSelected(SegmentedControl segmentedControl, int index)
        {
            AllowScreenMovement = false;

            this.CallAndHandleAction(AllowScreenMovementClicked, nameof(AllowScreenMovementClicked), false);
        }
    }
}
