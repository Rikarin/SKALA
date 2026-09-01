sealed class MentionFixture {
    // GC.Collect() is what this comment is about, and a comment is not a call.
    public string Advice() => "GC.Collect()";
}
