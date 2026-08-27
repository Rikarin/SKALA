// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
// Hazard 2 of docs/plan/05 § "Phase 4". A doc comment that is not well-formed XML is left exactly
// as it is and reported at hint (SK0003), never "fixed" — malformed doc comments are extremely
// common in real code, invisible to the compiler in a NoWarn-ed build, and the first thing a
// re-wrapping formatter would destroy. The oracle leaves them alone because it leaves every doc
// comment alone (SK-DIV-0006); Skala leaves them alone even under `format --xmldoc`, which is the
// one place the two agree for different reasons.

class C {
    /// <summary>Not closed <b>at all.</summary>
    void Unclosed() { }

    /// <summary>Mismatched</remarks>
    void Mismatched() { }

    /// <summary>A bare & ampersand.</summary>
    void BareAmpersand() { }

    ///<summary>Well-formed, and 128 columns wide, which the oracle does not wrap and which Skala only wraps when asked to.</summary>
    void Long() { }
}
