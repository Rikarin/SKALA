using System;

class C {
    bool Scheme(string value) => string.Equals(value, "https", StringComparison.InvariantCulture);
    bool Prefix(string key) => key.StartsWith("sk.", StringComparison.InvariantCulture);
    bool Suffix(string name) => name.EndsWith(".cs", StringComparison.InvariantCultureIgnoreCase);
    bool Instance(string a, string b) => a.Equals(b, StringComparison.InvariantCultureIgnoreCase);
}
