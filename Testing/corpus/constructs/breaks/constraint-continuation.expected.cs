// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-16
namespace Constructs.Breaks;

// SK-DIV-0105 (issue #370). The constraints inside one `where` clause continue on the `where`'s own
// column: a break the author wrote before or after a comma is kept there, and a clause too wide for
// the margin wraps at the last comma that fits, on the same column. Before this file the gaps had no
// plan, keep_user_linebreaks kept them and the clause's frame spent a level on each, one indent past
// the `where`. Measured at both values of skala_indent_type_constraints and of
// skala_place_type_constraints_on_same_line and at skala_continuous_indent_multiplier = 2: the
// constraint follows the `where` wherever the keys put it.
public class ConstraintContinuation {
    void BeforeTheComma<T1>()
        where T1 : class
        , new() { }

    void AfterTheComma<T1>()
        where T1 : class,
        new() { }

    void OnItsOwnLine<T1>()
        where T1 : class
        , new() { }

    void Twice<T1>()
        where T1 : class
        , System.IDisposable
        , new() { }

    void SecondClause<T1, T2>()
        where T1 : class
        , new()
        where T2 : struct { }

    void Overflowing<T1>()
        where T1 : System.Collections.Generic.IEnumerable<int>, System.Collections.Generic.IList<int>,
        System.IDisposable, new() { }

    void WithABody<T1>()
        where T1 : class
        , new() {
        int x = 0;
    }

    void Whole<T1>() where T1 : class, new() { }
}

public class OnAType<T1>
    where T1 : class
    , new() { }

public class OnATypeAfter<T1>
    where T1 : class,
    new() { }
