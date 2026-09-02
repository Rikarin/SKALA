using System.Text.RegularExpressions;

// ⚠ Serilog's `KeyValuePairSettings.CallableDirectiveRegex`, verbatim. A quantified group whose body
// carries a quantifier — the shape a naive detector matches — and it cannot blow up, because `{0,1}`
// admits at most one iteration. This file is why the outer quantifier has to be unbounded.
public static class Settings {
    const string CallableDirective =
        @"^(?<directive>audit-to|write-to|enrich|filter|destructure):(?<method>[A-Za-z0-9]*)(\.(?<argument>[A-Za-z0-9]*)){0,1}$";

    public static bool IsCallable(string input) => Regex.IsMatch(input, CallableDirective);
}
