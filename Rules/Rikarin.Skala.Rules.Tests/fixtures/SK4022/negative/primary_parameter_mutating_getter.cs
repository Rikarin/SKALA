// ⚠ A property getter that is not `readonly` runs on a defensive copy under `readonly struct`. One
// that mutates is rare and legal, and reading it is what changes here (#412).
public struct Ticket {
    public int Issued;

    public int Next {
        get {
            Issued++;
            return Issued;
        }
    }
}

struct Dispenser(Ticket ticket) {
    public int Take() => ticket.Next;

    public int Issued => ticket.Issued;
}

public static class Probe {
    public static int Run() {
        var dispenser = new Dispenser(new Ticket());
        dispenser.Take();
        return dispenser.Issued;
    }
}
