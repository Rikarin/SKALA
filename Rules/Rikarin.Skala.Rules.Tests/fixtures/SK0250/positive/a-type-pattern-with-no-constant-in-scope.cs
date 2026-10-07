// The other half of #424's constant capture: with nothing but the type called `Shape` in scope the
// bare type pattern binds the type, and the fix is offered.
public sealed class Shape { }

public static class Classifier {
    public static string Arm(object o) =>
        o switch {
            Shape _ => "shape",
            _ => "other"
        };
}
