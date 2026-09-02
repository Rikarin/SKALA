// ⚠ #302's shape (#325). The guard asked over the returned expression's FULL span, so a comment
// between the `=>` and the `null` — the natural place to justify returning it — silenced the rule.
// The fix replaces `null` with `string.Empty` and touches nothing above it.
namespace Fixtures {
    sealed class Ticket {
        public override string? ToString() =>
            // there is genuinely nothing to show for a ticket nobody has issued yet
            null;
    }
}
