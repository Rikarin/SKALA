// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
// Two defaults the #443 measurement turned up. An author's break after `is` or `as` before a type is
// kept with the point before the operator left whole — Skala joined it, and once kept it broke before the
// operator too. A filled list's closer the author put on its own line stays one level in when the first
// item shares the opener's line — Skala brought it back to the opener's level. The controls: a break
// after `is` before a pattern, after `is not` and after `case`, which both tools keep, and a tuple that
// broke after its `(`, whose `)` returns to the opener's level. (At keep_user_linebreaks = false all of
// these are joined; that half is pinned by KeepFalseLeftoversIssue443Tests.)

public class BreakAfterIsAndAKeptCloser {
    void T(object o) {
        var i1 = o is
            BreakAfterIsAndAKeptCloser;
        var i2 = o as
            BreakAfterIsAndAKeptCloser;
        var i3 = o is
            (1, 2);
        var i4 = o is not
            null;
        var q1 = o is BreakAfterIsAndAKeptCloser(1, 2
            );
        var t1 = (1, 2
            );
        var (d1, d2
            ) = (1, 2);
        var t2 = (
            1, 2
        );
        switch (o) {
            case
                (1, 2):
                break;
        }
    }
}
