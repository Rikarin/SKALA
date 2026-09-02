// ⚠ The regression this batch's probe file found. `PropertyDeclarationSyntax.DescendantTrivia`
// reaches the property's *leading* trivia, so a guard asked of the whole declaration silenced the
// rule on every documented property — and a documented property is most of them.
public sealed class Documented {
    /// <summary>The scheme every request uses.</summary>
    public string Scheme { get; } = "https";
}
