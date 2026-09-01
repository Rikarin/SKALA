// SK1063 has its own opinion about a literal in a hole. Two rules rewriting one span is one of
// them being wrong.
public sealed class Holes {
    public string Message() => $"{"a\\b"}";
}
