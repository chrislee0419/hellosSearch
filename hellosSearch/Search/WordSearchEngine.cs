using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.ComponentModel;
using IPA.Utilities.Async;
using SongCore;
using SongCore.Data;
using HUI.Utilities;

namespace HUISearch.Search
{
    public class WordSearchEngine
    {
        public IEnumerable<IPreviewBeatmapLevel> CachedResult => SearchFinished ? (IEnumerable<IPreviewBeatmapLevel>)_searchSpace : Array.Empty<IPreviewBeatmapLevel>();

        private LinkedList<IPreviewBeatmapLevel> _searchSpace;

        private CancellationTokenSource _cts = null;

        public bool SearchFinished { get; private set; } = false;
        public bool IsSearching => _cts != null;

        /// <summary>
        /// Start a new search query on a provided song list search space.
        /// </summary>
        /// <param name="searchSpace">A list of songs to search through</param>
        /// <param name="searchQuery">The test to search for</param>
        /// <param name="action">A callback to </param>
        public void StartNewSearch(IEnumerable<IPreviewBeatmapLevel> searchSpace, string searchQuery, Action<IEnumerable<IPreviewBeatmapLevel>> action)
        {
            if (searchSpace == null || searchSpace.Count() < 1 || action == null)
                return;

            StopSearch();

            SearchFinished = false;
            _searchSpace = new LinkedList<IPreviewBeatmapLevel>(searchSpace);

            StartSearchThread(searchQuery, action);
        }

        /// <summary>
        /// Used to query an already filtered song list. Only use this method to further refine an existing search result. 
        /// Otherwise, start a new search using <see cref="StartNewSearch"/>.
        /// </summary>
        /// <param name="searchQuery"></param>
        /// <param name="action"></param>
        public void StartSearchOnExistingList(string searchQuery, Action<IEnumerable<IPreviewBeatmapLevel>> action)
        {
            if (action == null)
            {
                return;
            }
            else if (_searchSpace == null || _searchSpace.Count() < 1)
            {
                action?.Invoke(Array.Empty<IPreviewBeatmapLevel>());
                return;
            }

            StopSearch();

            SearchFinished = false;

            StartSearchThread(searchQuery, action);
        }

        private void StartSearchThread(string searchQuery, Action<IEnumerable<IPreviewBeatmapLevel>> action)
        {
            _cts = new CancellationTokenSource();
            CancellationToken cancellationToken = _cts.Token;

            ThreadPool.QueueUserWorkItem(delegate (object state)
            {
                SearchSongsInternal(searchQuery, cancellationToken);

                if (!cancellationToken.IsCancellationRequested)
                {
                    UnityMainThreadTaskScheduler.Factory.StartNew(delegate ()
                    {
                        SearchFinished = true;

                        if (_cts != null)
                        {
                            _cts.Dispose();
                            _cts = null;
                        }

                        action?.Invoke(_searchSpace);
                    });
                }
            });
        }

        public void StopSearch()
        {
            if (!IsSearching)
                return;

            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        private void SearchSongsInternal(string searchQuery, CancellationToken cancellationToken)
        {
            string[] queryWords;
            bool stripSymbols = PluginConfig.Instance.StripSymbols;
            bool splitWords = PluginConfig.Instance.SplitQueryByWords;
            SearchableSongFields songFields = PluginConfig.Instance.SongFieldsToSearch;

            if (stripSymbols)
                searchQuery = StringUtilities.RemoveSymbolsRegex.Replace(searchQuery.ToLower(), string.Empty);
            else
                searchQuery = searchQuery.ToLower();

            if (splitWords)
                queryWords = searchQuery.Split(StringUtilities.SpaceCharArray, StringSplitOptions.RemoveEmptyEntries);
            else
                queryWords = new string[] { searchQuery };

            LinkedListNode<IPreviewBeatmapLevel> node = _searchSpace.First;
            while (node != null && !cancellationToken.IsCancellationRequested)
            {
                bool keep = CheckSong(node.Value, stripSymbols, splitWords, songFields, queryWords);
                LinkedListNode<IPreviewBeatmapLevel> nextNode = node.Next;

                if (!keep)
                    _searchSpace.Remove(node);

                node = nextNode;
            }
        }

        /// <summary>
        /// Check whether a song contains the all the words in the query.
        /// </summary>
        /// <param name="level">Song to check.</param>
        /// <param name="stripSymbols">Whether to strip the symbols of each word before checking.</param>
        /// <param name="combineSingleLetterSequences">Whether to combine single letter 'word' sequences.</param>
        /// <param name="songFields">Song fields to check.</param>
        /// <param name="queryWords">List of words (or a phrase) to query for.</param>
        /// <returns>True if the song contains all the words in the query, otherwise false.</returns>
        private bool CheckSong(IPreviewBeatmapLevel level, bool stripSymbols, bool combineSingleLetterSequences, SearchableSongFields songFields, IEnumerable<string> queryWords)
        {
            StringBuilder fieldsSB = new StringBuilder();
            if ((songFields & SearchableSongFields.SongName) != 0)
            {
                StringBuilder songName;

                if (combineSingleLetterSequences)
                {
                    songName = CombineSingleLetterSequences(level.songName).Append(' ');
                    if (!string.IsNullOrWhiteSpace(level.songSubName))
                        songName.Append(CombineSingleLetterSequences(level.songSubName)).Append(' ');
                }
                else
                {
                    songName = new StringBuilder(level.songName).Append(' ');
                    if (!string.IsNullOrWhiteSpace(level.songSubName))
                        songName.Append(CombineSingleLetterSequences(level.songSubName)).Append(' ');
                }

                fieldsSB.Append(songName);
            }

            if ((songFields & SearchableSongFields.SongAuthor) != 0)
                fieldsSB.Append(level.songAuthorName).Append(' ');
            if ((songFields & SearchableSongFields.LevelAuthor) != 0)
                fieldsSB.Append(level.levelAuthorName).Append(' ');

            if ((songFields & SearchableSongFields.Contributors) != 0 && level is CustomPreviewBeatmapLevel customLevel)
            {
                ExtraSongData extraSongData = Collections.RetrieveExtraSongData(BeatmapUtilities.GetCustomLevelHash(customLevel));
                if (extraSongData?.contributors != null)
                {
                    foreach (var contributor in extraSongData.contributors)
                        fieldsSB.Append(contributor._name).Append(' ');
                }
            }

            string fields = fieldsSB.ToLower().ToString();
            if (stripSymbols)
                fields = StringUtilities.RemoveSymbolsRegex.Replace(fields, string.Empty);

            foreach (var word in queryWords)
            {
                if (!fields.Contains(word))
                    return false;
            }

            return true;
        }

        private StringBuilder CombineSingleLetterSequences(string str)
        {
            // combine contiguous single letter 'word' sequences in the title each into one word
            // should only done when Split Words option is enabled
            StringBuilder sb = new StringBuilder(str.Length);
            StringBuilder constructedWordSB = new StringBuilder(str.Length);

            foreach (string word in str.Split(StringUtilities.SpaceCharArray, StringSplitOptions.RemoveEmptyEntries))
            {
                if (word.Length > 1 || !char.IsLetterOrDigit(word[0]))
                {
                    // multi-letter word or special character
                    if (constructedWordSB.Length > 0)
                    {
                        if (sb.Length > 0)
                            sb.Append(' ');

                        sb.Append(constructedWordSB);
                        constructedWordSB.Clear();
                    }

                    if (sb.Length > 0)
                        sb.Append(' ');

                    sb.Append(word);

                }
                else
                {
                    // single letter 'word'
                    constructedWordSB.Append(word);
                }
            }

            // add last constructed word if it exists
            if (constructedWordSB.Length > 0)
            {
                if (sb.Length > 0)
                    sb.Append(' ');

                sb.Append(constructedWordSB);
            }

            return sb;
        }

        [Flags]
        public enum SearchableSongFields
        {
            [Description("Song Name")]
            SongName = 1,

            [Description("Song Author(s)")]
            SongAuthor = 2,

            [Description("Level Authors")]
            LevelAuthor = 4,

            [Description("Level Contributors")]
            Contributors = 8
        }
    }
}
