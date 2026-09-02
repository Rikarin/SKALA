using System;

class C {
    int First(string haystack) => haystack.IndexOf("needle");
    int Last(string haystack) => haystack.LastIndexOf("-");
    bool Prefix(string key) => key.StartsWith("sk.");
    bool Suffix(string name) => name.EndsWith(".cs");
    int FromOffset(string haystack) => haystack.IndexOf("needle", 4);
    int InRange(string haystack) => haystack.LastIndexOf("needle", 8, 4);
}
