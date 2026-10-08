// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaDocComments generated=2026-10-08
class AttributeStyle {
    /// <remarks>
    ///     <see cref="System.String" href="https://short.invalid/" />
    /// </remarks>
    void AHeaderThatFits() { }

    /// <remarks>
    ///     <see cref="System.String"
    ///         href="https://short.invalid/" />
    /// </remarks>
    void AHeaderTheAuthorBroke() { }

    /// <param name="a">A single attribute.</param>
    void OneAttribute(int a) { }

    /// <summary>Text.</summary>
    /// <customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5"
    ///     zetaAttribute="6" />
    void AHeaderPastTheMargin() { }
}
