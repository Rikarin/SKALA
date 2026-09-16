// A member access whose receiver is an integer literal — `120.DegreesCelsius()`, `1.ToString()`,
// `0x1F.ToString()` — occurred nowhere in the corpus, and that absence is how #367 survived: a
// `SpaceRules.MustSeparate` rule forced `1 .ToString()` for two commits on the claim that `1.` would
// otherwise lex as the start of a real literal, and the only thing that ever measured it was its own
// unit test. The claim is false (the lexer takes the dot only when a digit follows it) and the rule
// is gone, but that is proof against the lexer and the compiler; this file is the proof against the
// oracle. Every shape is written twice, closed up and with the space the old rule inserted, so the
// fixture answers both "is the space kept" and "is it inserted". `50.0.CubicCentimetersPerSecond()`
// is the real-literal control that 530f2a23 already closed up.
static class Units {
    public static double DegreesCelsius(this int value) => value;

    public static double CubicCentimetersPerSecond(this double value) => value;
}

class MemberAccessOnNumericLiterals {
    string Decimal() => 1.ToString();

    string DecimalSpaced() => 1 .ToString();

    double Extension() => 120.DegreesCelsius();

    double ExtensionSpaced() => 120 .DegreesCelsius();

    string Separated() => 1_000.ToString();

    string SeparatedSpaced() => 1_000 .ToString();

    string Hexadecimal() => 0x1F.ToString();

    string HexadecimalSpaced() => 0x1F .ToString();

    double Real() => 50.0.CubicCentimetersPerSecond();

    double RealSpaced() => 50.0 .CubicCentimetersPerSecond();

    void Statements() {
        var celsius = 120.DegreesCelsius();
        var spaced = 120 .DegreesCelsius();
        var text = 1_000 .ToString() + 0x1F .ToString();
        var rate = 50.0 .CubicCentimetersPerSecond();
    }
}
