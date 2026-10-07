// ⚠ #423: the initializer runs a getter on every allocation, and deleting it stops the getter
// running. Measured for #412's audit: `calls=1` before the fix, `calls=0` after.
public static class Counter {
    public static int Calls;

    public static int Next {
        get {
            Calls++;
            return Calls;
        }
    }
}

public sealed class Ticket {
    int number = Counter.Next;

    public Ticket() {
        number = 3;
    }

    public int Number => number;
}

public static class Probe {
    public static int Run() => new Ticket().Number * 10 + Counter.Calls;
}
