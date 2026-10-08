class AttributeStyle {
    /// <remarks>
    /// <see cref="System.String" href="https://short.invalid/" />
    /// </remarks>
    void AHeaderThatFits() { }

    /// <remarks>
    /// <see cref="System.String"
    ///     href="https://short.invalid/" />
    /// </remarks>
    void AHeaderTheAuthorBroke() { }

    /// <param name="a">A single attribute.</param>
    void OneAttribute(int a) { }

    /// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" zetaAttribute="6" />
    void AHeaderPastTheMargin() { }
}
