// ⚠ No `using System;`, which is the case the fix has to survive: an edit that emits a bare
// `StringComparison` here turns a warning into CS0103, and EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic
// re-binds the fixed text, so this fixture is what proves the qualification is chosen and not assumed.
class Bare {
    int Find(string haystack) => haystack.IndexOf("needle");
    bool Prefix(string key) => key.StartsWith("sk.");
}
