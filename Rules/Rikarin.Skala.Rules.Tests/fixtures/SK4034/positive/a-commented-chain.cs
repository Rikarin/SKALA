// ⚠ The third copy of #302's defect, in `CallShape.ContainsComment`. The comment below is leading
// trivia of `entries`, which makes it part of the FULL span of the `OrderBy` call the guard is asked
// about — and the old walk covered FullSpan, so the rule declined.
//
// ⚠ It was never the question this rule needs answered. SK4034 swaps two member-name-to-end spans
// and moves the text verbatim, so what it must not step on is a comment INSIDE either call or in the
// dot between them. A comment on the line above is not moved by any edit the rule emits, and the
// negatives that pin the real hazard — `a-comment-between-the-calls` and friends — must keep
// declining, which is why both directions are held.
using System.Collections.Generic;
using System.Linq;

public sealed class Feed {
    public static IEnumerable<int> Recent(List<int> entries) =>
        // Sorting before the filter sorts entries that are about to be thrown away.
        entries.OrderBy(entry => entry).Where(entry => entry > 0);
}
