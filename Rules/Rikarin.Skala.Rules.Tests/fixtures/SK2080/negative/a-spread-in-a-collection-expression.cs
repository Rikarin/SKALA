using System.Collections.Generic;

public sealed class Names {
    // ⚠ A spread contributes elements the analyzer cannot see. One anywhere in the expression
    // withdraws the whole finding rather than being stepped over: a duplicate the spread supplies is
    // not this rule's to report, and one it hides is not this rule's to claim.
    public static HashSet<string> Merge(IEnumerable<string> extra) => ["alpha", .. extra, "alpha"];
}
