// SK0234's case: the operand already has the cast's type. This rule requires the negation.
public sealed class Same {
    public string Get(string text) {
        var copy = (string)text;
        return copy;
    }
}
