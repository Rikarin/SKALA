// The policy already spelled the way the rule asks for, plus the two current-culture members, which
// are `SK2010`'s subject and not this rule's: a stated CurrentCulture is a decision about display.
using System;

class C {
    bool Scheme(string value) => string.Equals(value, "https", StringComparison.Ordinal);
    bool Prefix(string key) => key.StartsWith("sk.", StringComparison.OrdinalIgnoreCase);
    bool Suffix(string name) => name.EndsWith(".cs", StringComparison.CurrentCulture);
    bool Instance(string a, string b) => a.Equals(b, StringComparison.CurrentCultureIgnoreCase);
    int First(string haystack) => haystack.IndexOf("needle", StringComparison.Ordinal);
}
