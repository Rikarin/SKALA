// ⚠ The false-positive class that decides this rule. `CultureInfo.InvariantCulture` is *correct*
// here: invariant is what round-trips formatted data, and a rule that could not tell a comparison
// from a conversion would be advising authors to corrupt their own serialisation. The exclusion is
// structural — this rule keys on the `System.StringComparison` enum, which none of these APIs take.
using System;
using System.Globalization;

class C {
    string Render(decimal value) => value.ToString(CultureInfo.InvariantCulture);
    string Formatted(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
    decimal Read(string text) => decimal.Parse(text, CultureInfo.InvariantCulture);
    DateTime Stamp(string text) => DateTime.Parse(text, CultureInfo.InvariantCulture);
    string Join(int a, int b) => string.Format(CultureInfo.InvariantCulture, "{0}/{1}", a, b);
    string Upper(string value) => value.ToUpper(CultureInfo.InvariantCulture);
}
