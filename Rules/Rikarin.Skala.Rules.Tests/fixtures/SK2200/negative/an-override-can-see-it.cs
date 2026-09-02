// ⚠ Field initializers run *before* the base constructor call, so a base constructor that calls a
// virtual member this type overrides reads the initialized value. The initializer is not dead.
public abstract class Widget {
    protected Widget() {
        Describe();
    }

    protected abstract void Describe();
}

public sealed class Slider : Widget {
    readonly int step = 1;

    public Slider(int given) {
        step = given;
    }

    protected override void Describe() {
        System.Console.WriteLine(step);
    }
}
