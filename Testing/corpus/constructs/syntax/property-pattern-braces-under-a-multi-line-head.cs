// A property pattern's braces break open whenever the pattern they belong to already spans lines at its
// `{`, however short the subpatterns are: a type argument list the formatter filled or the author broke,
// a positional clause after a broken type, a designation after the braces, a switch arm's type, behind
// `not`, and a subpattern whose colon the author broke. Skala opened them only for that last one, a source
// fact, so pass one joined braces behind a colon break it added itself and pass two then broke them (fuzz
// 1044487512275255493). The added colon break is pinned by PropertyPatternBracesUnderAMultiLineHeadTests:
// the oracle moves a long name's colon below it instead at some widths, which no Skala plan reproduces.
// The control: behind `not`, the colon's break is not the pattern's and its braces stay joined.
public class PropertyPatternBracesUnderAMultiLineHead {
    void T(object o) {
        switch (o) {
            case PropertyPatternBracesUnderAMultiLineHead { Pqqqqqqqqqqq: Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww, List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> { P149: not null } }:
                break;
            case bool {
                P146: Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww,
                    List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> { P149: not null }
            }:
                break;
            case bool { P146:
                Dictionary<int, int> { P149: not null } }:
                break;
            case Dictionary<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww, List<TimeSpan_wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww>> { P149: not null }:
                break;
            case bool { P146: Foo<int,
                int> { P149: not null } x }:
                break;
            case bool { P146: not Foo<int,
                int> { P149: not null } }:
                break;
            case bool { P146: Foo<int,
                int>(1, 2) { P149: not null } }:
                break;
            case bool { LongName:
                not Foo { P149: not null } }:
                break;
        }

        var r = o switch {
            Dictionary<int,
                int> { P149: not null } => 1,
            _ => 2
        };
    }
}
