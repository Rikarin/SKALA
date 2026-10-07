// The override keeps the base's parameter names, so a named argument through the derived type binds
// the same way with or without it.
public class Shape {
    public string Last = "";

    public virtual void Move(int dx, int dy) => Last = "dx=" + dx + " dy=" + dy;
}

public class Square : Shape {
    public override void Move(int dx, int dy) => base.Move(dx, dy);
}

public static class Probe {
    public static string Run() {
        var square = new Square();
        square.Move(dy: 2, dx: 1);
        return square.Last;
    }
}
