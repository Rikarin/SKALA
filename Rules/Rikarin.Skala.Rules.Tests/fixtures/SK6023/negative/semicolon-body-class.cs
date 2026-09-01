namespace Fixture;

// ⚠ The semicolon body is a statement that the type is complete with nothing in it, and C# 12
// allows it on every type declaration rather than only on records. `AnalysisTests` uses exactly
// this shape as its "nothing to report" fixture.
public sealed class Clean;

public struct Marker;

public interface IEmpty;
