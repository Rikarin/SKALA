// ⚠ #431: the base constructor stores `this` without running any code, and the constructor then throws
// before the overwrite. Whoever holds the stored instance reads the initialized value. Measured `5`
// before the fix and `0` after.
public sealed class Registry {
    public object? Last;
}

public class Entry {
    protected Entry(Registry registry) {
        registry.Last = this;
    }
}

public sealed class Ratio : Entry {
    int value = 5;

    public Ratio(Registry registry, int divisor) : base(registry) {
        var quotient = 10 / divisor;
        value = quotient;
    }

    public int Value => value;
}

public static class Probe {
    public static int Run() {
        var registry = new Registry();
        try {
            _ = new Ratio(registry, 0);
        } catch (System.DivideByZeroException) {
            // The instance escaped through the registry before the throw.
        }

        return ((Ratio)registry.Last!).Value;
    }
}
