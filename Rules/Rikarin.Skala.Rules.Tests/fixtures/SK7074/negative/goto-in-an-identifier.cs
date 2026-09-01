// The word, not the statement. A rule matching text rather than syntax would report every line here.
public sealed class Work {
    const string Goto = "goto";

    public string Describe(string label) => "goto " + label + " // " + Goto;

    /// <summary>Mentions goto in prose, and in <c>goto label;</c> form.</summary>
    public int GotoCount { get; set; }
}
