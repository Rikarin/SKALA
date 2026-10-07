// ⚠ #431: the base constructor calls an override, and the override reaches the field through a private
// helper — so the field's name is never written in the override itself. Measured `5` before the fix and
// `0` after. A base constructor that runs any code is now enough to decline.
public abstract class Widget {
    public int Seen;

    protected Widget() {
        Hook();
    }

    protected abstract void Hook();
}

public sealed class Slider : Widget {
    int step = 5;

    public Slider(int given) {
        step = given;
    }

    protected override void Hook() => Print();

    void Print() => Seen = step;

    public int Step => step;
}

public static class Probe {
    public static int Run() => new Slider(1).Seen;
}
