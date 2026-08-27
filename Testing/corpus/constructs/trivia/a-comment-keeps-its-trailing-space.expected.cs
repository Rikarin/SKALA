// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
class C {
    /// <summary>
    ///     A doc comment line whose text ends in a space.
    /// </summary>
    /// <remarks>Another one.</remarks>
    public int Kept { get; set; }

    // A line comment ending in a space. 
    void M() {
        var x = 1; // and a trailing one. 
    }
}
