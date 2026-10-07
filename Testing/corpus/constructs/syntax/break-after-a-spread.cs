// An author's break after a spread's or a slice pattern's `..` (#439). The oracle keeps it, chops the
// list, and puts the operand on the element's own column; Skala gave the operand a continuation level of
// its own. A break before and after a range's `..` and before a positional pattern's `(` are the
// controls: the oracle keeps those with one level, as Skala already did. (At keep_user_linebreaks =
// false every one of them is joined; constructs run at the defaults, so that half is pinned by
// JoinAtKeepFalseIssue439Tests.)
public class BreakAfterASpread {
    void T(object o, int[] a) {
        int[] s1 = [1, ..
            a];
        var l1 = a is [1, ..
            var rest];
        var r1 = a[1
            ..2];
        var r2 = a[1..
            2];
        var q1 = o is BreakAfterASpread
            (1, 2);
    }
}
