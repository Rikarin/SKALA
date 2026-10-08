// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaDocComments generated=2026-10-08
class WrapTagsAndPi {
    /// <summary>
    ///     Some prose that runs on for long enough that the inline element which follows it cannot stay on the same line
    ///     <see cref="System.String" /> as written.
    /// </summary>
    void M() { }

    /// <remarks>
    ///     <see cref="System.Collections.Generic.Dictionary{TKeyOfSomeVeryLongName,TValueOfSomeVeryLongName}"
    ///         href="https://example.invalid/a/very/long/documentation/link/that/will/not/fit" />
    /// </remarks>
    void HeaderPastTheMargin() { }

    /// <remarks>
    ///     <see cref="System.String"
    ///         href="https://short.invalid/" />
    /// </remarks>
    void AnAuthorsBreakKeptAtBothValues() { }
}
