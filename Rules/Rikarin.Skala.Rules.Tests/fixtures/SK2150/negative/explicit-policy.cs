using System;
using System.Globalization;

class C {
    int First(string haystack) => haystack.IndexOf("needle", StringComparison.Ordinal);
    int Last(string haystack) => haystack.LastIndexOf("-", StringComparison.OrdinalIgnoreCase);
    bool Prefix(string key) => key.StartsWith("sk.", StringComparison.CurrentCulture);
    bool Suffix(string name) => name.EndsWith(".cs", StringComparison.InvariantCulture);
    bool Cultured(string key) => key.StartsWith("sk.", true, CultureInfo.InvariantCulture);
    int FromOffset(string haystack) => haystack.IndexOf("needle", 4, StringComparison.Ordinal);
}
