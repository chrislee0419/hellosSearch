using System;
using System.Runtime.CompilerServices;
using IPA.Config.Stores;
using IPA.Config.Stores.Attributes;
using IPA.Config.Stores.Converters;
using HUI.Utilities;
using static HUISearch.Search.WordSearchEngine;

[assembly: InternalsVisibleTo(GeneratedStore.AssemblyVisibilityTarget)]
namespace HUISearch
{
    internal class PluginConfig
    {
        public event Action ConfigReloaded;

        public static PluginConfig Instance { get; set; }

        public virtual bool CloseScreenOnSelectLevel { get; set; } = CloseScreenOnSelectLevelDefaultValue;
        public const bool CloseScreenOnSelectLevelDefaultValue = true;

        public virtual bool CloseScreenOnSelectLevelCollection { get; set; } = CloseScreenOnSelectLevelCollectionDefaultValue;
        public const bool CloseScreenOnSelectLevelCollectionDefaultValue = true;

        public virtual bool ClearQueryOnSelectLevelCollection { get; set; } = ClearQueryOnSelectLevelCollectionDefaultValue;
        public const bool ClearQueryOnSelectLevelCollectionDefaultValue = false;

        public virtual bool UseOffHandLaserPointer { get; set; } = UseOffHandLaserPointerDefaultValue;
        public const bool UseOffHandLaserPointerDefaultValue = true;

        public virtual bool StripSymbols { get; set; } = StripSymbolsDefaultValue;
        public const bool StripSymbolsDefaultValue = false;

        public virtual bool SplitQueryByWords { get; set; } = SplitQueryByWordsDefaultValue;
        public const bool SplitQueryByWordsDefaultValue = true;

        [UseConverter(typeof(EnumConverter<SearchableSongFields>))]
        public virtual SearchableSongFields SongFieldsToSearch { get; set; } = SongFieldsToSearchDefaultValue;
        public const SearchableSongFields SongFieldsToSearchDefaultValue =
            SearchableSongFields.SongName | SearchableSongFields.SongAuthor | SearchableSongFields.LevelAuthor | SearchableSongFields.Contributors;

        /// <summary>
        /// This is called whenever BSIPA reads the config from disk (including when file changes are detected).
        /// </summary>
        public virtual void OnReload() => this.CallAndHandleAction(ConfigReloaded, nameof(ConfigReloaded));
    }
}