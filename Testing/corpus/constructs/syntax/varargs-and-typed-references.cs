// ArgListExpression, MakeRefExpression, RefTypeExpression and RefValueExpression occurred nowhere.
// They are four of the thirty-seven absent node kinds and they are cheap to pin: `__arglist(…)` and
// `__refvalue(…, T)` both carry argument lists, which is the one thing about them a formatter can get
// wrong. Nobody writes them on purpose, but a formatter that mangles them corrupts a file, and after
// the oracle is gone there is no other way to find that out.
class VarargsAndTypedReferences {
    static void Variadic(int first, __arglist) {
        var iterator = __arglist;
    }

    static void Called(int alpha, int bravo, string charlie) {
        Variadic(0, __arglist());
        Variadic(0, __arglist(alpha, bravo, charlie));
        Variadic(0, __arglist(alpha, bravo, charlie, alpha, bravo, charlie, alpha, bravo, charlie, alpha, bravo));
    }

    static string Typed(ref int origin) {
        var reference = __makeref(origin);
        var kind = __reftype(reference);
        var value = __refvalue(reference, int);
        return kind.Name + value.ToString();
    }

    static int Nested(ref int alpha, ref int bravo) =>
        __refvalue(__makeref(alpha), int) + __refvalue(__makeref(bravo), int);
}
