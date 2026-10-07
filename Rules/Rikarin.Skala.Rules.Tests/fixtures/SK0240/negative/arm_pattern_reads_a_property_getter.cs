// ⚠ #412's audit: the arm's property pattern calls `P`'s getter, and the discard arm does not.
// Deleting the arm leaves `Reads` at 0.
public sealed class Box {
    public int Reads;

    public int P {
        get {
            Reads++;
            return 1;
        }
    }
}

public static class Probe {
    public static string Run() {
        var box = new Box();
        var result = box switch {
            { P: 1 } => "x",
            _ => "x"
        };
        return result + " " + box.Reads;
    }
}
