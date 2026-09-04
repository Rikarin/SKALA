// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class InterpolatedStrings {
    string Fits(string alpha) => $"alpha is {alpha}";

    string VerbatimField =
        $@"the alpha value is one and the bravo value is two and the charlie value is three and it overflows";

    void VerbatimLocal(string alpha) {
        var assigned =
            $@"the alpha value is {alpha} and the bravo value is two and the charlie value is three and it overflows";
        Consume(
            $@"the alpha value is {alpha} and the bravo value is two and the charlie value is three and this overflows"
        );
    }

    string RegularExpressionBody(string alpha, string bravo) =>
        $"the alpha value is {alpha} and the bravo value is {bravo} and this interpolated string overflows the margin";

    string Concatenated(string alpha) {
        return $@"the alpha value is {alpha} and the bravo value is two"
            + $@" and the charlie value is three and it overflows";
    }

    string SpansLines(string alpha) {
        return $@"first line of the verbatim string with {alpha} in it
second line of the verbatim string which is also quite long and runs past the margin";
    }

    string NestedHole(string alpha, int count) =>
        $"alpha is {alpha} repeated {count.ToString().PadLeft(4, '0')} times over and over";

    void Consume(string value) { }
}
