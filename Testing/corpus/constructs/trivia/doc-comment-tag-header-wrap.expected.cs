// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
class TagHeaderWrap {
    /// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" zetaAttribute="6">Body.</customElement>
    void WrappedAtTheLastAttributeThatFits() { }

    /// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" zetaAttribute="6" />
    void SelfClosingWrapped() { }

    /// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" zetaAttribute="6" etaAttribute="7" thetaAttribute="8" iotaAttribute="9" kappaAttribute="10" lambdaAttribute="11" muAttribute="12" nuAttribute="13" xiAttribute="14" />
    void FilledGreedilyOverSeveralLines() { }

    /// <summary>Text.</summary><customElementWithAVeryLongName alphaAttributeWithAVeryLongNameIndeed="1111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111111" beta="2" />
    void TheFirstAttributeMovesTooWhenItCannotFit() { }

    /// <remarks>
    /// <see cref="System.Collections.Generic.Dictionary{TKeyOfSomeVeryLongName,TValueOfSomeVeryLongName}" href="https://example.invalid/a/very/long/documentation/link/that/will/not/fit" />
    /// </remarks>
    void NestedContinuationIsOneIndentPastTheTag() { }

    /// <summary>Some prose here <see cref="System.Collections.Generic.Dictionary{TKeyOfSomeVeryLongName,TValueOfSomeVeryLongName}" href="https://example.invalid/a/very/long/documentation/link" /> after.</summary>
    void MovedOffTheProseLineThenWrapped() { }

    /// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" z="xxxx">Body.</customElement>
    void TheClosingAngleIsNotCounted() { }

    /// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" z="xxxxx">Body.</customElement>
    void OneColumnMore() { }

    /// <remarks>
    /// <see cref="System.String"
    ///   href="https://example.invalid/a/very/long/documentation/link/that/is/really/long" title="a title that is long enough to go past" />
    /// </remarks>
    void AnAuthorsBreakIsKeptAndTheRestWrapped() { }

    /// <remarks>
    /// <see cref="System.String" href="https://short.invalid/"
    ///  />
    /// </remarks>
    void ABreakBeforeTheCloserIsJoined() { }

    /// <remarks>
    /// <see
    /// cref="System.String" />
    /// </remarks>
    void ABreakBeforeTheFirstAttributeIsKept() { }

    /// <summary>Text.</summary>
    /// <customElement alphaAttribute="1"
    ///  betaAttribute="2">Body.</customElement>
    void AShortHeaderTheAuthorBrokeStaysBroken_AndOpensItsElement() { }

    /// <summary>Text.</summary>
    /// <customElement alphaAttribute="1"
    ///  betaAttribute="2" />
    void SelfClosingKept() { }

    /// <summary>Text.</summary>
    /// <customElement
    ///  alphaAttribute="1" betaAttribute="2" />
    void TheRestOfTheLineAfterAKeptBreakStaysTogether() { }

    /// <summary>wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww</summary>
    void OneLongWordIsNotOpened() { }

    /// <summary>wwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwww b</summary>
    void TwoWordsAre() { }

    /// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" z="xxx">Body.</customElement>
    void AFullHeaderBeforeOneWordStaysFlat() { }

    /// <summary>Text.</summary><customElement alphaAttribute="1" betaAttribute="2" gammaAttribute="3" deltaAttribute="4" epsilonAttribute="5" z="xxx">Body more words here.</customElement>
    void AFullHeaderBeforeSeveralWordsOpens() { }
}
