using System;
using System.Globalization;

class C {
    int M(string a, string b) => string.Compare(a, b, StringComparison.CurrentCulture);
    int N(string a, string b) => string.Compare(a, b, true, CultureInfo.InvariantCulture);
    bool P(string a, string b) => a.ToLower(CultureInfo.CurrentCulture) == b;
}
