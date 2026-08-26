namespace Rikarin.Skala.Formatting;

/// <summary>
/// A half-open range of characters in a source file.
/// </summary>
/// <remarks>
/// ⚠ docs/plan/04 § "The document IR" writes <c>Anchor(TextSpan Source, int TokenId)</c>, but
/// <c>TextSpan</c> lives in <c>Microsoft.CodeAnalysis.Text</c> and docs/plan/02 forbids this
/// project a Roslyn reference. The two statements cannot both hold, so the span type is defined
/// here and the C# front end converts. See the M1 report's "what the plan got wrong" section.
/// </remarks>
public readonly record struct SourceSpan(int Start, int Length) {
    public int End => Start + Length;

    public bool IsEmpty => Length == 0;

    public static SourceSpan FromBounds(int start, int end) => new(start, end - start);

    public bool IntersectsWith(SourceSpan other) => other.Start <= End && other.End >= Start;

    public override string ToString() =>
        $"[{Start.ToString(System.Globalization.CultureInfo.InvariantCulture)}..{End.ToString(System.Globalization.CultureInfo.InvariantCulture)})";
}
