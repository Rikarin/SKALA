// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class C {
    // ⚠ Same trap as a-field-the-formatter-chops, reached the other way: the attribute goes to its
    // own line, so the field is two lines in the output and one in the input.
    [ThreadStatic]
    static int First;

    [ThreadStatic]
    static int Second;

    int Plain;
}
