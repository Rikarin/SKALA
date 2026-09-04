// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
using System;
using System.Collections.Generic;

/// <summary>
///     IndexerMemberCref, OperatorMemberCref, ConversionOperatorMemberCref, ExtensionMemberCref and
///     CrefBracketedParameterList occurred nowhere in the corpus; only NameMemberCref and
///     QualifiedCref did. A cref is parsed syntax rather than text, so the doc-comment sub-formatter
///     walks it — which means every one of these forms is a shape it can move, and none of them had a
///     fixture.
/// </summary>
/// <remarks>
///     ⚠ In <c>constructs/syntax/</c> rather than <c>constructs/xmldoc/</c>, and the fixture beside it
///     is therefore the <b>format-only</b> answer alone. <c>constructs/xmldoc/</c> is one file per
///     option key by an invariant <c>XmlDocOracleTests</c> asserts — the doc-comment verdict keys off
///     the file name — so a fixture named after a *construct* cannot live there without attributing
///     its measurement to nothing. What this pins is that the format-only profile returns every cref
///     form untouched and formats the code around them, which is a real pin and a weaker one than the
///     doc-comment profile would give. See docs/construct-coverage.md § "The cref forms are pinned
///     under one profile of two" for what is still unmeasured and what it would take.
/// </remarks>
class CrefMemberForms {
    /// <summary>The plain forms, for the pair the others are read against.</summary>
    /// <seealso cref="CrefMemberForms" />
    /// <seealso cref="Method(int)" />
    /// <seealso cref="Generic{T}(T)" />
    /// <seealso cref="System.Collections.Generic.IReadOnlyList{T}.Count" />
    public int Method(int subject) => subject;

    /// <summary>A generic method, whose cref carries a type-argument list.</summary>
    public T Generic<T>(T subject) => subject;

    /// <summary>
    ///     The indexer form, <see cref="this[int]" />, and the qualified one,
    ///     <see cref="CrefMemberForms.this[string, int]" />, whose parameter list is bracketed rather
    ///     than parenthesised — the only place CrefBracketedParameterList can appear.
    /// </summary>
    public int this[int index] => index;

    /// <summary>The two-parameter indexer, referenced above.</summary>
    public int this[string key, int fallback] => fallback;

    /// <summary>
    ///     The operator form: <see cref="operator +(CrefMemberForms, CrefMemberForms)" />,
    ///     <see cref="operator ==(CrefMemberForms, CrefMemberForms)" /> and the one whose parameter
    ///     list is wide enough that the element has to be wrapped,
    ///     <see cref="operator *(CrefMemberForms, CrefMemberForms)" />.
    /// </summary>
    public static CrefMemberForms operator +(CrefMemberForms left, CrefMemberForms right) => left;

    /// <summary>Referenced by the operator cref above.</summary>
    public static CrefMemberForms operator *(CrefMemberForms left, CrefMemberForms right) => left;

    /// <summary>Referenced by the operator cref above.</summary>
    public static bool operator ==(CrefMemberForms left, CrefMemberForms right) => ReferenceEquals(left, right);

    /// <summary>Referenced by the operator cref above.</summary>
    public static bool operator !=(CrefMemberForms left, CrefMemberForms right) => !(left == right);

    /// <summary>
    ///     The conversion form: <see cref="explicit operator int(CrefMemberForms)" /> and
    ///     <see cref="implicit operator string(CrefMemberForms)" />.
    /// </summary>
    public static explicit operator int(CrefMemberForms subject) => 0;

    /// <summary>Referenced by the conversion cref above.</summary>
    public static implicit operator string(CrefMemberForms subject) => string.Empty;

    /// <inheritdoc cref="Method(int)" />
    public override bool Equals(object? other) => base.Equals(other);

    /// <inheritdoc cref="object.GetHashCode" />
    public override int GetHashCode() => base.GetHashCode();
}

/// <summary>
///     ExtensionMemberCref — C# 14's <c>cref="T.extension(receiver).Member"</c>. ⚠ Kept in this
///     fixture rather than assumed unreachable: the doc-comment profile was measured to indent and
///     whitespace-normalise <see cref="Extensions.extension(string).IsBlank" /> exactly as it does an
///     ordinary <see cref="Extensions.Ordinary(int)" />, so the sub-formatter really does walk it.
/// </summary>
/// <seealso cref="Extensions.extension(string).Repeated(int)" />
/// <seealso cref="Extensions.extension(System.Collections.Generic.IReadOnlyList{string}).Largest" />
static class Extensions {
    extension(string subject) {
        public bool IsBlank => subject.Length == 0;

        public string Repeated(int times) => string.Concat(System.Linq.Enumerable.Repeat(subject, times));
    }

    extension(IReadOnlyList<string> subjects) {
        public string Largest => subjects.Count == 0 ? string.Empty : subjects[^1];
    }

    public static int Ordinary(int value) => value;
}
