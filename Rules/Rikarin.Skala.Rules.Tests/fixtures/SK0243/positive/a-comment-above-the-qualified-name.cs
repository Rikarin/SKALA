using System.Text;

public static class Building {
    public static int Length(StringBuilder builder) {
        // ⚠ A comment ABOVE the finding, not inside the span the fix deletes. #302: a guard that
        // asks a *node* whether it holds a comment reads the leading trivia of its first token,
        // and a `//` or a `///` on the line above is exactly that — so the rule goes silent on
        // code it should report, and the silence looks like clean code.
        System.Text.StringBuilder local = builder;
        return local.Length;
    }
}
