using Commands;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Utils.Search.Trie;

namespace Terminal.Search;

public class CommandSearch
{
    private readonly IEnumerable<ICommandAction> _commandActions;

    private class JSONLine
    {
        public string Word { get; set; }
        public string Key { get; set; }
        public string Pos { get; set; }
        public string[] Synonyms { get; set; }
    }

    private string[] _dictionaryEnglish = new string[0];

    private ConcurrentDictionary<string, string[]> ThesaurusIndex { get; set; } = new();
    private TrieSearch? AutocompleteTrie { get; set; }

    private ConcurrentDictionary<string, ICommandAction[]> CommandActionIndex { get; set; } = new();
    private TrieSearch? CommandActionMetadataTrie { get; set; }

    public CommandSearch(IEnumerable<ICommandAction> commandActions)
    {
        _commandActions = commandActions;
    }

    public void AsynchronouslyLoadIndexes()
    {
        Task.Run(async () => await LoadAndIndexDocumentation());
        Task.Run(async () => await LoadJsonDataAsync());
    }

    private async Task LoadAndIndexDocumentation()
    {
        CommandActionMetadataTrie = new(_commandActions.Select(a => a.Profile.Name).ToArray());

        foreach (ICommandAction action in _commandActions)
        {
            var associatedWords =
                GetWords(action.Profile.Name)
                    .Concat(GetWords(action.Profile.Description))
                    .Concat(GetWords(action.Profile.KeyWords));

            foreach (var word in associatedWords)
            {
                CommandActionIndex.AddOrUpdate(word, new ICommandAction[] { action }, (_, existing) =>
                {
                    // Merge synonyms for the same key
                    return existing.Append(action).ToArray();
                });
            }
        }
    }

    private static string[] GetWords(string text)
    {
        return text.Split(new char[] { ' ', '\r', '\n', '\t' })
                   .Select(x => x.Trim().ToLower())
                   .Where(w => !string.Equals(w, "", StringComparison.Ordinal))
                   .ToArray();
    }

    private async Task LoadJsonDataAsync()
    {
        // Assuming your JSONL file is an embedded resource named "yourresource.jsonl"
        var assembly = Assembly.GetExecutingAssembly();
        using var thesaurusResource = assembly.GetManifestResourceStream("Terminal.Search.en_thesaurus.jsonl");
        if (thesaurusResource == null)
        {
            throw new Exception("Resource not found : Terminal.Search.en_thesaurus.jsonl");
        }

        using var thesaurusReader = new StreamReader(thesaurusResource);
        string line;

        int percent = -1;
        double lineCount = 0;
        double totalLines = 169010;

        while ((line = await thesaurusReader.ReadLineAsync()) != null)
        {
            lineCount++;
            int newPercent = (int)((lineCount / totalLines) * 100);
            if (percent != newPercent)
            {
                Debug.WriteLine($"{percent}%");
                percent = newPercent;
            }

            // Deserialize each line into your model
            JSONLine item = JsonConvert.DeserializeObject<JSONLine>(line);

            if (item != null)
            {
                string key = item.Key;

                // Strip the _1, _2, etc a the end of the key string
                int underscoreIndex = key.LastIndexOf('_');
                if (underscoreIndex > 0)
                {
                    key = key.Substring(0, underscoreIndex);
                }

                // Check if the key already exists in the dictionary
                ThesaurusIndex.AddOrUpdate(key, item.Synonyms, (_, existingSynonyms) =>
                {
                    // Merge synonyms for the same key
                    return existingSynonyms.Concat(item.Synonyms).Distinct().ToArray();
                });
            }
        }

        using var dictionaryResource = assembly.GetManifestResourceStream("Terminal.Search.dictionary-en.csv");
        if (dictionaryResource == null)
        {
            throw new Exception("Resource not found : Terminal.Search.dictionary-en.csv");
        }

        using var dictionaryReader = new StreamReader(dictionaryResource);

        _dictionaryEnglish = ReadLines(dictionaryReader).ToArray();

        var keys = _dictionaryEnglish.ToArray();
        AutocompleteTrie = new TrieSearch(keys);
    }

    private IEnumerable<string> ReadLines(StreamReader s)
    {
        string? line = s.ReadLine();
        while (line != null)
        {
            yield return line;
            line = s.ReadLine();
        }
    }

    private void PrintSampleOfList(List<string> list)
    {
        Debug.Write($"    [");
        for (int i = 0; i < Math.Min(5, list.Count); i++)
        { 
            if (i > 0)
            {
                Debug.Write($", ");
            }

            Debug.Write(list[i]);
        }

        if (list.Count > 5)
        {
            Debug.WriteLine($"...]");
        }
        else
        {
            Debug.WriteLine("]");
        }
    }

    private static HashSet<string> _unusefullyCommonSearchTerms = new()
    {
        "and",
        "the",
        "of",
        "to",
        "a",
        "in",
        "is",
        "you",
        "that",
        "it",
        "he",
        "was",
        "for",
        "on",
        "are",
        "as",
        "with",
        "his",
        "they",
        "I",
        "at",
        "be",
        "this",
        "have",
        "from",
        "or",
        "had",
        "by",
        "but",
        "were",
        "we",
        "an",
        "your",
        "which",
        "she",
        "do",
        "their",
        "will",
        "there",
        "would",
        "can",
        "so",
    };

    public IEnumerable<string> GetSuggestions(string word)
    {
        Debug.WriteLine("");

        // Not yet initialised in background task, just do basic search for now...
        if (AutocompleteTrie == null || CommandActionMetadataTrie == null)
            return Enumerable.Empty<string>();

        // Direct autocomplete of commands
        List<string> commandAutocompleteResult = AutocompleteOfCurrentWord(word);

        // Autocomplete from thesaurus words
        List<string> dictionaryAutocompleteResults = AutocompleteSearchOnDictionary(word);

        // If we find nothing, try fuzzy a search that attempts to correct some types of spelling mistake
        // TODO Once there are weights, we can use these result systematically but with a lower weight
        dictionaryAutocompleteResults = FuzzyAutocompleteSerachOnDictionary(word, dictionaryAutocompleteResults);

        // Do a thesaurus search on the results but include the original autocompleted results as well
        List<string> synonyms = SearchForSynonyms(dictionaryAutocompleteResults);

        // Search for commands based on their metadata
        List<string> commandNames = SearchForCommandNames(word, dictionaryAutocompleteResults, synonyms);

        // Compile the results
        var finalRsults =
            commandAutocompleteResult.Concat(commandNames)
                                     .Distinct()
                                     .ToList();

        Debug.WriteLine($"(finalRsults) Comining the autocomplete search on current incomplete command name and associated command names found via a synonym search on their metadata we found {finalRsults.Count} results");
        PrintSampleOfList(finalRsults);

        return finalRsults;
    }

    private List<string> AutocompleteOfCurrentWord(string word)
    {
        var commandAutocompleteResult = 
            CommandActionMetadataTrie.Search(word)
                                     .Except(_unusefullyCommonSearchTerms)
                                     .ToList();

        Debug.WriteLine($"(commandAutocompleteResult) Autocomplete search on current incomplete command name produced {commandAutocompleteResult.Count} results");
        PrintSampleOfList(commandAutocompleteResult);
        return commandAutocompleteResult;
    }

    private List<string> AutocompleteSearchOnDictionary(string word)
    {
        List<string> dictionaryAutocompleteResults = 
            AutocompleteTrie.Search(word)
                            .Except(_unusefullyCommonSearchTerms)
                            .ToList();

        Debug.WriteLine($"(dictionaryAutocompleteResults) Autocomplete search on en dict produced {dictionaryAutocompleteResults.Count} results");
        PrintSampleOfList(dictionaryAutocompleteResults);
        return dictionaryAutocompleteResults;
    }

    private List<string> FuzzyAutocompleteSerachOnDictionary(string word, List<string> dictionaryAutocompleteResults)
    {
        if (dictionaryAutocompleteResults.Count == 0)
        {
            Debug.WriteLine($"Trying fuzzy search");

            dictionaryAutocompleteResults = 
                AutocompleteTrie.FuzzySearch(word)
                                .Except(_unusefullyCommonSearchTerms)
                                .ToList();

            Debug.WriteLine($"(autocompleteResults) Fuzzy autocomplete search on en dict produced {dictionaryAutocompleteResults.Count} results");
            PrintSampleOfList(dictionaryAutocompleteResults);
        }

        return dictionaryAutocompleteResults;
    }

    private List<string> SearchForSynonyms(List<string> dictionaryAutocompleteResults)
    {
        var synonyms =
            dictionaryAutocompleteResults.SelectMany(WordToSynonyms) // Thesaurus results
                                         .Except(_unusefullyCommonSearchTerms)
                                         .ToList();

        Debug.WriteLine($"(synonyms) Found {synonyms.Count} synonyms");
        PrintSampleOfList(synonyms);
        return synonyms;
    }

    private List<string> SearchForCommandNames(string word, List<string> dictionaryAutocompleteResults, List<string> synonyms)
    {
        List<string> commandNames = new();
        var useCombinedSearch = false;
        if (useCombinedSearch)
        {
            // All the terms to use to do the documentation search
            var searchTerms =
                Enumerable.Empty<string>()
                          .Append(word) // Use the original word to search in the metadata
                          .Concat(dictionaryAutocompleteResults) // Use the autocomplete results to search in the metadata
                          .Concat(synonyms) // Use the synonyms to search in the metadata
                          .Distinct()
                          .ToList();

            Debug.WriteLine($"(searchTerms) Combined distinct synonyms, autocomplete results and the original word : {searchTerms.Count}");
            PrintSampleOfList(searchTerms);


            // Search for commands based of thesuraus results and the command metadata
            commandNames =
                synonyms.SelectMany(WordToAssociatedCommandNames) // Search the command metadata
                        .Distinct()
                        .ToList();

            Debug.WriteLine($"(commandNames) Found {commandNames.Count} associated command names via their metadata");
            PrintSampleOfList(commandNames);
        }
        else
        {
            // Original word
            var commandNamesFromOriginalWord =
                WordToAssociatedCommandNames(word).ToList();
            Debug.WriteLine($"(commandNamesFromOriginalWord) {commandNamesFromOriginalWord.Count}");
            PrintSampleOfList(commandNamesFromOriginalWord);


            // Autocomplete results
            List<string> commandNamesFromAutocompleteList =
                dictionaryAutocompleteResults.SelectMany(WordToAssociatedCommandNames) // Search the command metadata
                                             .Distinct()
                                             .ToList();
            Debug.WriteLine($"(commandNamesFromAutocompleteList) {commandNamesFromAutocompleteList.Count}");
            PrintSampleOfList(commandNamesFromAutocompleteList);


            // Synonyms
            List<string> commandNamesFromSynonyms =
                synonyms.SelectMany(WordToAssociatedCommandNames)
                                             .Distinct()
                                             .ToList();
            Debug.WriteLine($"(commandNamesFromSynonyms) {commandNamesFromSynonyms.Count}");
            PrintSampleOfList(commandNamesFromSynonyms);

            commandNames =
                commandNamesFromOriginalWord
                    .Concat(commandNamesFromAutocompleteList)
                    .Concat(commandNamesFromSynonyms)
                    .Distinct()
                    .ToList();
        }

        return commandNames;
    }

    private IEnumerable<string> WordToSynonyms(string word)
    {
        if (ThesaurusIndex.TryGetValue(word, out string[]? synonyms))
        {
            return synonyms;
        }
        else
        {
            return Array.Empty<string>();
        }
    }

    private IEnumerable<string> WordToAssociatedCommandNames(string word)
    {
        if (CommandActionIndex.TryGetValue(word, out ICommandAction[]? actions))
        {
            return actions.Select(a => a.Profile.Name);
        }
        else
        {
            return Array.Empty<string>();
        }
    }
}
