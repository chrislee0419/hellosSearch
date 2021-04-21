using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using SongCore;
using SongCore.Data;
using HUISearch.Search.Data;
using HUI.Utilities;

namespace HUISearch.Search
{
    // NOTE: the words stored are all converted to lowercase
    internal class LevelCollectionWordStorage
    {
        public bool IsLoading { get; private set; } = false;
        public bool IsReady { get; private set; } = false;

        private Trie _trie = new Trie();
        private Dictionary<string, WordInformation> _words = new Dictionary<string, WordInformation>();
        private BKTree _bkTree = new BKTree();

        private HMTask _task;
        private ManualResetEvent _manualResetEvent;
        private bool _taskCancelled;

        public LevelCollectionWordStorage()
        { }

        public LevelCollectionWordStorage(IAnnotatedBeatmapLevelCollection levelCollection)
        {
            SetupStorage(levelCollection);
        }

        public LevelCollectionWordStorage(IEnumerable<IPreviewBeatmapLevel> levels)
        {
            SetupStorage(levels);
        }

        /// <summary>
        /// Populate word storage with the words in the song name, sub-name, author, and map creator of a level pack.
        /// </summary>
        /// <param name="levelCollection">The level collection whose words you want to store.</param>
        public void SetupStorage(IAnnotatedBeatmapLevelCollection levelCollection)
        {
            IsLoading = true;
            _manualResetEvent = new ManualResetEvent(true);
            _taskCancelled = false;

            _task = new HMTask(
                delegate ()
                {
                    var sw = Stopwatch.StartNew();
                    Plugin.Log.Info($"Creating word storage for the \"{levelCollection.collectionName}\" level collection (contains {levelCollection.beatmapLevelCollection.beatmapLevels.Length} songs)");

                    if (!SetWordsFromLevels(levelCollection.beatmapLevelCollection.beatmapLevels))
                    {
                        sw.Stop();
                        return;
                    }

                    sw.Stop();
                    Plugin.Log.Info($"Finished creating word storage for the \"{levelCollection.collectionName}\" level collection (took {sw.ElapsedMilliseconds / 1000f} seconds, {_words.Count} unique words processed)");
                },
                delegate ()
                {
                    _manualResetEvent = null;
                    _task = null;
                    IsLoading = false;
                });

            _task.Run();
        }

        public void SetupStorage(IEnumerable<IPreviewBeatmapLevel> levels)
        {
            IsLoading = true;
            _manualResetEvent = new ManualResetEvent(true);
            _taskCancelled = false;

            _task = new HMTask(
                delegate ()
                {
                    var sw = Stopwatch.StartNew();
                    Plugin.Log.Info($"Creating word storage for an unlabelled level collection (contains {levels.Count()} songs)");

                    if (!SetWordsFromLevels(levels))
                    {
                        sw.Stop();
                        return;
                    }

                    sw.Stop();
                    Plugin.Log.Info($"Finished creating word storage for the unlabelled level collection (took {sw.ElapsedMilliseconds / 1000f} seconds, {_words.Count} unique words processed)");
                },
                delegate ()
                {
                    _manualResetEvent = null;
                    _task = null;
                    IsLoading = false;
                });

            _task.Run();
        }

        /// <summary>
        /// Pause the setup task if it exists.
        /// </summary>
        public void PauseSetup()
        {
            if (!IsLoading || _manualResetEvent == null || _task == null)
                return;

            _manualResetEvent.Reset();
            Plugin.Log.Info("Blocking word count storage setup thread");
        }

        /// <summary>
        /// Resume the setup task if it exists.
        /// </summary>
        public void ResumeSetup()
        {
            if (!IsLoading || _manualResetEvent == null || _task == null)
                return;

            _manualResetEvent.Set();
            Plugin.Log.Info("Resuming word count storage setup thread");
        }

        /// <summary>
        /// Cancels the setup task if it exists.
        /// </summary>
        public void CancelSetup()
        {
            if (!IsLoading || _manualResetEvent == null || _task == null)
                return;

            _taskCancelled = true;
            _manualResetEvent.Set();
            Plugin.Log.Info("Cancelling word count storage setup thread");
        }

        private bool SetWordsFromLevels(IEnumerable<IPreviewBeatmapLevel> levelCollection)
        {
            // we don't build the _words object immediately only because we want to also add the
            // counts of other words that are prefixed by a word to the count
            List<string> allWords = new List<string>();
            Dictionary<string, Dictionary<string, int>> allWordConnections = new Dictionary<string, Dictionary<string, int>>();

            foreach (var level in levelCollection)
            {
                _manualResetEvent.WaitOne();
                if (_taskCancelled)
                    return false;

                string[] songNameWords = GetWordsFromString(level.songName).ToArray();
                string[] songSubNameWords = GetWordsFromString(level.songSubName).ToArray();
                string[] authorNameWords = GetWordsFromString(level.songAuthorName).ToArray();
                IEnumerable<string> levelAuthors = GetWordsFromString(level.levelAuthorName);
                IEnumerable<string> contributors = Array.Empty<string>();

                if (level is CustomPreviewBeatmapLevel customLevel)
                {
                    ExtraSongData extraSongData = Collections.RetrieveExtraSongData(BeatmapUtilities.GetCustomLevelHash(customLevel));
                    if (extraSongData?.contributors != null)
                        contributors = extraSongData.contributors.Select(x => x._name).Where(x => !string.IsNullOrEmpty(x));
                }

                string[][] wordsFromSong = new string[][]
                {
                    songNameWords,
                    songSubNameWords,
                    authorNameWords
                };

                foreach (string[] wordsFromField in wordsFromSong)
                {
                    for (int i = 0; i < wordsFromField.Length; ++i)
                    {
                        string currentWord = wordsFromField[i];
                        allWords.Add(currentWord);

                        if (!allWordConnections.ContainsKey(currentWord))
                            allWordConnections[currentWord] = new Dictionary<string, int>();

                        if (i + 1 < wordsFromField.Length)
                        {
                            string nextWord = wordsFromField[i + 1];
                            var connections = allWordConnections[currentWord];

                            if (connections.ContainsKey(nextWord))
                                connections[nextWord] += 1;
                            else
                                connections.Add(nextWord, 1);
                        }
                    }
                }

                // last word of song name connects to the first word of subname and all mappers
                string lastWord = songNameWords.LastOrDefault();
                if (!string.IsNullOrEmpty(lastWord))
                {
                    var connections = allWordConnections[lastWord];
                    string[] connectionWords = levelAuthors.Append(songSubNameWords.FirstOrDefault()).Concat(contributors).ToArray();

                    foreach (var firstWord in connectionWords)
                    {
                        // only make a connection once (same thing for the below connections)
                        if (!string.IsNullOrEmpty(firstWord) && !connections.ContainsKey(firstWord))
                            connections.Add(firstWord, 1);
                    }
                }

                // last word of song subname connects to first word of author
                lastWord = songSubNameWords.LastOrDefault();
                if (!string.IsNullOrEmpty(lastWord))
                {
                    var connections = allWordConnections[lastWord];
                    var authorFirstWord = authorNameWords.FirstOrDefault();

                    if (!string.IsNullOrEmpty(authorFirstWord) && !connections.ContainsKey(authorFirstWord))
                        connections.Add(authorFirstWord, 1);
                }

                // last word of author name connects to first word of song name
                lastWord = authorNameWords.LastOrDefault();
                if (!string.IsNullOrEmpty(lastWord))
                {
                    var connections = allWordConnections[lastWord];
                    var songNameFirstWord = songNameWords.FirstOrDefault();

                    if (!string.IsNullOrEmpty(songNameFirstWord) && !connections.ContainsKey(songNameFirstWord))
                        connections.Add(songNameFirstWord, 1);
                }

                // level authors/contributors are added to the word storage differently from the other fields
                var firstSongNameWord = songNameWords.FirstOrDefault();
                foreach (string mapper in levelAuthors.Concat(contributors))
                {
                    // since the names of map makers occur very frequently, we limit them to only one entry
                    // otherwise, they always show up as the first couple of predictions
                    if (!allWords.Contains(mapper))
                        allWords.Add(mapper);

                    Dictionary<string, int> levelAuthorConnections;
                    if (!allWordConnections.ContainsKey(mapper))
                    {
                        levelAuthorConnections = new Dictionary<string, int>();
                        allWordConnections[mapper] = levelAuthorConnections;
                    }
                    else
                    {
                        levelAuthorConnections = allWordConnections[mapper];
                    }

                    // make connection between this mapper and the first word of the song name
                    if (!string.IsNullOrEmpty(firstSongNameWord) && !levelAuthorConnections.ContainsKey(firstSongNameWord))
                        levelAuthorConnections.Add(firstSongNameWord, 1);
                }
            }

            // sort by word length in descending order
            allWords.Sort((x, y) => y.Length - x.Length);

            // add words to the storage
            foreach (var word in allWords)
            {
                _manualResetEvent.WaitOne();
                if (_taskCancelled)
                    return false;

                if (_words.ContainsKey(word))
                {
                    _words[word].Count += 1;
                    continue;
                }

                // get count of words that have this word as a prefix
                int count = 1;
                foreach (var prefixedWord in _trie.StartsWith(word))
                    count += _words[prefixedWord].Count;

                _trie.AddWord(word);
                _words.Add(
                    word,
                    new WordInformation(count,
                        allWordConnections[word]
                        .OrderByDescending(x => x.Value)
                        .Select(p => p.Key)
                        .ToList())
                );
                _bkTree.AddWord(word);
            }

            IsReady = true;
            return true;
        }

        /// <summary>
        /// Get words that start with a given prefix, sorted by their counts in descending order.
        /// </summary>
        /// <param name="prefix">Find words that start with this prefix.</param>
        /// <returns>An <see cref="IEnumerable{string}"/> of words.</returns>
        public IEnumerable<string> GetWordsWithPrefix(string prefix)
        {
            if (!IsReady)
                return Array.Empty<string>();
            else
                return _trie.StartsWith(prefix.ToLower()).OrderByDescending(s => _words[s].Count);
        }

        /// <summary>
        /// Gets the words that appear after this word, sorted by occurence.
        /// </summary>
        /// <param name="word">Find words that appear after this word.</param>
        /// <returns>An <see cref="IEnumerable{string}"/> of words.</returns>
        public IEnumerable<string> GetFollowUpWords(string word)
        {
            if (!IsReady || !_words.TryGetValue(word.ToLower(), out var wordInfo))
                return Array.Empty<string>();

            return wordInfo.FollowUpWords;
        }

        /// <summary>
        /// Gets the words that partially match the provided word according to the Levenshtein distance. 
        /// The provided word must be 3 characters or longer.
        /// </summary>
        /// <param name="word">Find words that are close matches to this word. Exact matches are omitted.</param>
        /// <param name="tolerance">The largest Levenshtein distance for a word match to be accepted.</param>
        /// <returns>An <see cref="IEnumerable{string}"/> of similar words.</returns>
        public IEnumerable<string> GetFuzzyMatchedWords(string word, int tolerance = 2)
        {
            if (word.Length < 3 || tolerance < 1)
                return Array.Empty<string>();

            var words = _bkTree.Search(word, tolerance);

            if (words.Contains(word))
                words.Remove(word);

            return words;
        }

        /// <summary>
        /// Gets the words that partially match the provided word according to the Jaro-Winkler similarity. 
        /// The provided word must be 3 characters or longer.
        /// </summary>
        /// <param name="word">Find words that are close matches to this word. Exact matches are omitted.</param>
        /// <param name="minSimilarity">The minimum similarity value for a word match to be accepted, where 1 represents matching same word.</param>
        /// <returns>An <see cref="IEnumerable{string}"/> of similar words.</returns>
        public IEnumerable<string> GetFuzzyMatchedWordsAlternate(string word, float minSimilarity = 0.7f)
        {
            List<string> words = new List<string>();

            if (word.Length < 3 || minSimilarity <= 0f || minSimilarity >= 1f)
                return words;

            foreach (var kv in _words)
            {
                string targetWord = kv.Key;
                if (JaroWinklerSimilarity(word, targetWord) >= minSimilarity)
                    words.Add(targetWord);
            }

            if (words.Contains(word))
                words.Remove(word);

            return words;
        }

        private IEnumerable<string> GetWordsFromString(string s)
        {
            return StringUtilities.RemoveSymbolsRegex.Replace(s.ToLower(), " ").Split(StringUtilities.SpaceCharArray, StringSplitOptions.RemoveEmptyEntries).Where(x => x.Length > 2);
        }

        public static float JaroWinklerSimilarity(string s1, string s2, float scalingFactor = 0.1f, int maxCommonPrefix = 4)
        {
            // adapted from: https://stackoverflow.com/a/19165108
            if (string.IsNullOrEmpty(s1))
                return string.IsNullOrEmpty(s2) ? 1f : 0f;

            // get number of matched characters (m)
            float m = 0;
            int matchDistance = Convert.ToInt32(Math.Floor(Math.Max(s1.Length, s2.Length) / 2f)) - 1;
            bool[] matchedCharacters1 = new bool[s1.Length];
            bool[] matchedCharacters2 = new bool[s2.Length];
            for (int i = 0; i < s1.Length; ++i)
            {
                int left = Math.Max(0, i - matchDistance);
                int right = Math.Min(s2.Length - 1, i + matchDistance);

                for (int j = left; j <= right; ++j)
                {
                    if (s1[i] != s2[j] || matchedCharacters2[j])
                        continue;

                    matchedCharacters1[i] = true;
                    matchedCharacters2[j] = true;
                    ++m;

                    break;
                }
            }

            if (m == 0)
                return 0;

            // get number of transpositions (t)
            float t = 0;
            for (int i = 0, j = 0; i < s1.Length; ++i)
            {
                if (!matchedCharacters1[i])
                    continue;

                while (!matchedCharacters2[j])
                    ++j;

                if (s1[i] != s2[j])
                    ++t;

                ++j;
            }

            float jaro = ((m / s1.Length) + (m / s2.Length) + ((m - (t / 2)) / m)) / 3;

            // get length of common prefix (l)
            int l = 0;
            for (int i = 0; i < s1.Length && i < s2.Length && i < maxCommonPrefix; ++i)
            {
                if (s1[i] == s2[i])
                    ++l;
                else
                    break;
            }

            return jaro + l * scalingFactor * (1 - jaro);
        }

        private class WordInformation
        {
            /// <summary>
            /// Number of occurences of this word in the level pack.
            /// </summary>
            public int Count;

            /// <summary>
            /// Words that come immediately after this word, sorted by the number of occurences.
            /// </summary>
            public List<string> FollowUpWords;

            public WordInformation(int count, List<string> followUpWords)
            {
                Count = count;
                FollowUpWords = followUpWords;
            }
        }
    }
}
