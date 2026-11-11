// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-11-11
// Nightly fuzz 7817611539049315985: a pattern chain broken inside the arm pattern's own parenthesis keeps its `or`s
// one level past the arm when the arm lifts — an arrow broken for width, an arrow kept on its own line, a `when`
// with or without an argument list, a positional clause. The lift through inner breaks is the `when` clause's alone
// (measured 2026-11-11).

public class C {
    public object Width(int state) {
        while (state switch {
                   null => items,
                   not (0
                       or 1
                       or 2) when Emit("utf8"u8, 67581, @"verbatim\path", "sss") =>
                       $"n={items.Count:F2} and {items.Count}",
                   _ => (0xa33 ? null : false)
               }) { }
    }

    public object KeptArrow(int state) {
        return state switch {
            not (0
                or 1
                or 2) when Emit("utf8"u8, 67581, @"verbatim\path", "sssssssssssssssssssssssssssssssssssss")
                => $"n={state:F2} and {state}",
            _ => 2
        };
    }

    public object Chain(int state) {
        return state switch {
            not (0
                or 1
                or 2) when ok && readyyyyyyyyyyyyyyyy && sssssssssssssssssssssssssssssssssssssssssssssssssss =>
                $"n={state:F2} and {state}",
            _ => 2
        };
    }

    public object Name(int state) {
        return state switch {
            not (0
                or 1
                or 2) when okkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk =>
                $"n={state:F2} and {state}",
            _ => 2
        };
    }

    public object List(int state) {
        return state switch {
            not (0
                or 1
                or 2) when Emit("utf8"u8, 67581, @"verbatim\path", "sssssssssssssssssssssssssssss") =>
                $"n={state:F2} and {state} ssssssssssssssssssssssssssssssss",
            _ => 2
        };
    }

    public object Positional(int state) {
        return state switch {
            Point(0
                or 1, 2) when Emit("utf8"u8, 67581, @"verbatim\path", "sssssssssssssssssssssssssssss") =>
                $"n={state:F2} and {state} ssssssssssssssssssssssssssss",
            _ => 2
        };
    }
}
