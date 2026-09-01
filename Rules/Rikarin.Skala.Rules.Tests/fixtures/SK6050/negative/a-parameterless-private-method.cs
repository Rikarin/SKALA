namespace Contoso.Design;

// A private method with no parameters returning a constant is a named constant written as a method.
// There are no inputs to ignore, so the finding this rule makes has nothing to say about it.
public sealed class Limits {
    public int Budget() => Ceiling();

    static int Ceiling() => 4096;
}
