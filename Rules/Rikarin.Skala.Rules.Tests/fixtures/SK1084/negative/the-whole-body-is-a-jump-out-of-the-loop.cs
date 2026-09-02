using System.Collections.Generic;
using System.Linq;

// ⚠ #329 defect 3: the rewrite would leave a `foreach` whose entire body is `return null;`, which is
// SK2212 — a loop that cannot run more than once. One rule's fix handing the author another rule's
// finding is not a fix, and `EveryFix_SilencesTheRuleAndIntroducesNoDiagnostic` cannot see it,
// because it filters the post-fix diagnostics to the fixture's own rule id.
public sealed class Guardrail {
    public static string? FirstError(IEnumerable<int> severities) {
        foreach (var severity in severities) {
            if (severity > 0) {
                return null;
            }
        }

        return "clean";
    }
}
