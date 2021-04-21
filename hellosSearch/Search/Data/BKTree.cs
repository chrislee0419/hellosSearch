using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace HUISearch.Search.Data
{
    // adapted from: https://nullwords.wordpress.com/2013/03/13/the-bk-tree-a-data-structure-for-spell-checking/
    internal class BKTree
    {
        private Node _root;

        public void AddWord(string word)
        {
            if (_root == null)
            {
                _root = new Node(word);
                return;
            }

            Node curr = _root;

            int dist = LevenshteinDistance(curr.Word, word);
            while (curr.ContainsKey(dist))
            {
                if (dist == 0)
                    return;

                curr = curr[dist];
                dist = LevenshteinDistance(curr.Word, word);
            }

            curr.AddChild(dist, word);
        }

        public List<string> Search(string word, int tolerance = 2)
        {
            List<string> results = new List<string>();

            if (word == null || word.Length == 0 || _root == null)
                return results;

            Queue<Node> nodesToSearch = new Queue<Node>();
            nodesToSearch.Enqueue(_root);

            while (nodesToSearch.Count > 0)
            {
                Node curr = nodesToSearch.Dequeue();
                int dist = LevenshteinDistance(curr.Word, word);
                int minDist = dist - tolerance;
                int maxDist = dist + tolerance;

                if (dist <= tolerance)
                    results.Add(curr.Word);

                foreach (int key in curr.Keys.Where(key => key >= minDist && key <= maxDist))
                    nodesToSearch.Enqueue(curr[key]);
            }

            return results;
        }

        /// <summary>
        /// Get the Levenshtein edit distance between two strings.
        /// </summary>
        /// <param name="s1">The first string.</param>
        /// <param name="s2">The second string.</param>
        /// <returns>The edit distance between the two provided strings.</returns>
        public static int LevenshteinDistance(string s1, string s2)
        {
            if (s1.Length == 0)
                return s2.Length;
            else if (s2.Length == 0)
                return s1.Length;

            int[,] d = new int[s1.Length + 1, s2.Length + 1];

            for (int i = 0; i <= s1.Length; i++)
                d[i, 0] = i;

            for (int i = 0; i <= s2.Length; i++)
                d[0, i] = i;

            for (int i = 1; i <= s1.Length; i++)
            {
                for (int j = 1; j <= s2.Length; j++)
                {
                    int match = (s1[i - 1] == s2[j - 1]) ? 0 : 1;

                    d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + match);
                }
            }

            return d[s1.Length, s2.Length];
        }

        private class Node
        {
            public string Word { get; set; }
            public HybridDictionary Children { get; private set; }

            public Node()
            { }

            public Node(string word)
            {
                Word = word;
            }

            public Node this[int key]
            {
                get => (Node)Children[key];
            }

            public IEnumerable<int> Keys
            {
                get
                {
                    if (Children == null)
                        return new List<int>();
                    return Children.Keys.Cast<int>();
                }
            }

            public bool ContainsKey(int key) => Children != null && Children.Contains(key);

            public void AddChild(int key, string word)
            {
                if (Children == null)
                    Children = new HybridDictionary();
                Children[key] = new Node(word);
            }
        }
    }
}
