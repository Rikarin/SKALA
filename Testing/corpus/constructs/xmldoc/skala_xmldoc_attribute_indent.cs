class AttributeIndent {
    /// <remarks>
    /// <see cref="System.Collections.Generic.Dictionary{TKeyOfSomeVeryLongName,TValueOfSomeVeryLongName}" href="https://example.invalid/a/very/long/documentation/link/that/will/not/fit" />
    /// </remarks>
    void NestedTag() { }

    /// <summary>Text.</summary><customElementName alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" zetaAttribute="6" />
    void ShortName() { }

    /// <summary>Text.</summary><customElementWithAVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryVeryLongName alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" />
    void ANameThatPushesTheAlignmentPastTwoThirds() { }
}
