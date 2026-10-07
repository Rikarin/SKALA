// ⚠ #431: no override anywhere — the base constructor tests `this` for an interface and calls it, and
// the implementation reads the field. Measured `5` before the fix and `0` after. Declined twice: the
// base body runs code, and `this is IShow` hands `this` on, so a throw there would be observable too.
public interface IShow {
    void Show();
}

public class Panel {
    public int Seen;

    protected Panel() {
        if (this is IShow show) {
            show.Show();
        }
    }
}

public sealed class Gauge : Panel, IShow {
    int width = 5;

    public Gauge(int given) {
        width = given;
    }

    public void Show() => Seen = width;

    public int Width => width;
}

public static class Probe {
    public static int Run() => new Gauge(1).Seen;
}
