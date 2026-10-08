// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaDocComments generated=2026-10-08
class AttributeIndent {
    /// <remarks>
    ///     <see cref="System.Collections.Generic.Dictionary{TKeyOfSomeVeryLongName,TValueOfSomeVeryLongName}"
    ///         href="https://example.invalid/a/very/long/documentation/link/that/will/not/fit" />
    /// </remarks>
    void NestedTag() { }

    /// <summary>Text.</summary>
    /// <customElementName alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5"
    ///     zetaAttribute="6" />
    void ShortName() { }

    /// <summary>Text.</summary>
    /// <customElementWithAVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryLongName alphaAttribute="1" betaAttribute="2"
    ///     gammaAttribute="3" deltaAttribute="4" />
    void ANameThatPushesTheAlignmentPastTwoThirds() { }
}
