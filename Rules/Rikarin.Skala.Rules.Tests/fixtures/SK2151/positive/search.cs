using System;

class C {
    int First(string haystack) => haystack.IndexOf("needle", StringComparison.InvariantCulture);
    int Last(string haystack) => haystack.LastIndexOf("-", StringComparison.InvariantCultureIgnoreCase);
    bool Has(string haystack) => haystack.Contains("needle", StringComparison.InvariantCulture);
}
