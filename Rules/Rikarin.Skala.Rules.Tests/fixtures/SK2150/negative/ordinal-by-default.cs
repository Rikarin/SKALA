// ⚠ The false-positive class that decides this rule. Every call here is *already ordinal* on .NET:
// `Contains(string)` is ordinal, and so is every `char` overload of the four search methods.
// Reporting them would be advising the author to write down the behaviour they already have.
class C {
    bool Has(string haystack) => haystack.Contains("needle");
    int FirstChar(string haystack) => haystack.IndexOf('-');
    int LastChar(string haystack) => haystack.LastIndexOf('-');
    bool PrefixChar(string key) => key.StartsWith('s');
    bool SuffixChar(string name) => name.EndsWith('/');
    int FromOffset(string haystack) => haystack.IndexOf('-', 4);
}
