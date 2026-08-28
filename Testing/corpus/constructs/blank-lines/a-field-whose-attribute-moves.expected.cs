// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class C {
    // ⚠ Same trap as a-field-the-formatter-chops, reached the other way: the attribute goes to its
    // own line, so the field is two lines in the output and one in the input.
    [ThreadStatic]
    static int First;

    [ThreadStatic]
    static int Second;

    int Plain;
}
