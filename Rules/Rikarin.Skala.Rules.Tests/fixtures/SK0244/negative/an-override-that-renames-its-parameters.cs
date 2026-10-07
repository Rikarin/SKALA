// ⚠ #412's audit: a named argument through the derived type binds to the override's names. With the
// override `Move(dx: 1, dy: 2)` passes 2 first; deleted, it binds to the base's names and passes 1.
public class Shape {
    public string Last = "";

    public virtual void Move(int dx, int dy) => Last = "dx=" + dx + " dy=" + dy;
}

public class Square : Shape {
    public override void Move(int dy, int dx) => base.Move(dy, dx);
}

public static class Probe {
    public static string Run() {
        var square = new Square();
        square.Move(dx: 1, dy: 2);
        return square.Last;
    }
}
