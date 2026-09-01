namespace Contoso.Design;

// A closed hierarchy: only a nested type can reach the private constructor, and one does.
public class Result {
    private Result() { }

    public sealed class Ok : Result {
        public int Value { get; init; }
    }
}
