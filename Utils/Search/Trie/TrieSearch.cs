namespace Utils.Search.Trie;

public class TrieSearch
{
    //public struct Letter : IEqualityComparer<Letter>
    //{
    //    private const string _chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    //    private int Index;

    //    public static implicit operator Letter(char c)
    //    {
    //        return new Letter() { Index = _chars.IndexOf(c) };
    //    }

    //    public char ToChar()
    //    {
    //        return _chars[Index];
    //    }

    //    public override string ToString()
    //    {
    //        return _chars[Index].ToString();
    //    }

    //    public bool Equals(Letter x, Letter y)
    //    {
    //        return x.Index == y.Index;
    //    }

    //    public int GetHashCode([DisallowNull] Letter obj)
    //    {
    //        return obj.GetHashCode();
    //    }
    //}

    public class Node
    {
        public string Word;
        public bool IsTerminal { get { return Word != null; } }
        public Dictionary<char, Node> Edges = new Dictionary<char, Node>();
    }

    private class Trie
    {
        public Node Root = new Node();

        public Trie(string[] words)
        {
            for (int w = 0; w < words.Length; w++)
            {
                var word = words[w];
                var node = Root;
                for (int len = 1; len <= word.Length; len++)
                {
                    var letter = word[len - 1];
                    Node next;
                    if (!node.Edges.TryGetValue(letter, out next))
                    {
                        next = new Node();
                        if (len == word.Length)
                        {
                            next.Word = word;
                        }
                        node.Edges.Add(letter, next);
                    }
                    node = next;
                }
            }
        }
    }

    private Trie _trie;

    public TrieSearch(string[] words)
    {
        _trie = new(words);
    }

    public List<string> FuzzySearch(string word)
    {
        var node = _trie.Root;
        List<string> result = new List<string>();

        var chars = word.ToCharArray();

        FuzzyGenWords(node, chars, 0, result);
        return result;
    }

    private void FuzzyGenWords(Node n, char[] chars, int index, List<string> wordsFound)
    {
        if (index < 0)
        {
            return;
        }

        if (index >= chars.Length)
        {
            // Une fois qu'on a épuisé le début de mot, on souhaite retourner tous les mots en mode breadth first
            // On certain nodes in the trie this can become very expensive
            // Once we have weighting, we can choose to take only the most probable paths
            TrieToBreadthFirstList(n, wordsFound);
            return;
        }

        char c = chars[index];
        int numberBeforeSearch = wordsFound.Count;

        foreach (var edge in n.Edges)
        {
            if (c != edge.Key)
            {
                continue;
            }

            if (edge.Value.IsTerminal)
            {
                wordsFound.Add(edge.Value.Word);
            }

            // Probably excessive
            // Skip or un skip characters to try to take into account the user missing a character or typing a character twice
            //FuzzyGenWords(edge.Value, chars, index - 1, wordsFound); // Typing a character x3 ?
            FuzzyGenWords(edge.Value, chars, index, wordsFound); // Typing a character x2 ?
            FuzzyGenWords(edge.Value, chars, index + 1, wordsFound); // Nominal
            FuzzyGenWords(edge.Value, chars, index + 2, wordsFound); // Skipping a character

        }

        if (wordsFound.Count == numberBeforeSearch)
        {
            foreach (var edge in n.Edges)
            {
                // Try to take into account the user typing the wrong character
                FuzzyGenWords(edge.Value, chars, index + 1, wordsFound);
            }
        }
    }

    public List<string> Search(string word)
    {
        var node = _trie.Root;
        List<string> result = new List<string>();

        var chars = word.ToCharArray();

        GenWords(node, chars, 0, result);
        return result;
    }

    private void GenWords(Node n, char[] chars, int index, List<string> wordsFound)
    {
        if (index >= chars.Length)
        {
            // Une fois qu'on a épuisé le début de mot, on souhaite retourner tous les mots en mode breadth first
            TrieToBreadthFirstList(n, wordsFound);
            return;
        }

        char c = chars[index];

        foreach (var edge in n.Edges)
        {
            if (c != edge.Key)
            {
                continue;
            }

            if (edge.Value.IsTerminal)
            {
                //wordsFound.Add(edge.Value.Word);
            }

            GenWords(edge.Value, chars, index + 1, wordsFound);
        }
    }

    private void TrieToBreadthFirstList(Node n, List<string> wordsFound)
    {
        foreach (var value in n.Edges.Values)
        {
            if (value.IsTerminal)
            {
                wordsFound.Add(value.Word);
            }
            else
            {
                TrieToBreadthFirstList(value, wordsFound);
            }
        }
    }
}