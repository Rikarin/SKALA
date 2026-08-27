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
