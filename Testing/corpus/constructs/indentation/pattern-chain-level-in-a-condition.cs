// The level of a binary pattern chain's continuation inside and around a statement condition (#520).
// Directly the condition — through `is`, a parenthesised or negated pattern — the `or` sits on the
// condition's aligned column (`while (` aligns it to 15); as the operand of an `&&` or `||` it takes
// a level past the operand's line, as it does after `var b =` and `return`.
class C {
    void M(object o, bool x) {
        if (o is Alpha
            or Beta) { }
        if (x && o is Alpha
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
        Use(o is not (Alpha
            or Beta));
        Use(
            o is not (Alpha
            or Beta),
            x
        );
        return;
    }

    bool P(object o) => o is not (Alpha
        or Beta);

    bool Q(object o, bool x) {
        return x
            || o is Alpha
            or Beta;
    }
}
