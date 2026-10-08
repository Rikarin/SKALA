// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// The level of a binary pattern chain's continuation inside and around a statement condition (#520).
// Directly the condition — through `is`, a parenthesised or negated pattern — the `or` sits on the
// condition's aligned column (`while (` aligns it to 15); as the operand of an `&&` or `||` it takes
// a level past the operand's line, as it does after `var b =` and `return`. Under an `is` the author
// broke before, the `or`s take the `is`'s column.

class C {
    void M(object o, bool x) {
        if (o is Alpha
            or Beta) { }

        if (x
            && o is Alpha
                or Beta) { }

        if (x
            || o is Alpha
                or Beta) { }

        if (o is not (Alpha
            or Beta)) { }

        if (o is (Alpha
            or Beta)) { }

        if (x
            || o is not (Alpha
                or Beta)) { }

        while (o is not (Alpha
               or Beta)) { }

        var a = o is Alpha
            or Beta;
        var b = o is not (Alpha
            or Beta);
        var c = x
            || o is Alpha
                or Beta;
        var d = x
            || o is not (Alpha
                or Beta);
        Use(
            o is not (Alpha
                or Beta)
        );
        Use(
            o is not (Alpha
                or Beta),
            x
        );
        return;
    }

    bool P(object o) =>
        o is not (Alpha
            or Beta);

    bool Q(object o, bool x) {
        return x
            || o is Alpha
                or Beta;
    }

    bool R(Node next) {
        var x = next.Parent
            is Alpha
            or Beta;
        return next.Parent
            is not (Alpha
            or Beta);
    }
}
