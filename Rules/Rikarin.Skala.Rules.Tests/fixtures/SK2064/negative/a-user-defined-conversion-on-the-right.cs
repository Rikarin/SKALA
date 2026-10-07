// ⚠ #423: the right operand converts to `bool` through an operator somebody wrote, and the fix would
// skip calling it.
public readonly struct Flag {
    public static int Conversions;

    public static implicit operator bool(Flag flag) {
        Conversions++;
        return true;
    }
}

public static class Probe {
    public static int Run() {
        var a = Flag.Conversions < 0;
        var flag = new Flag();
        var both = a & flag;
        return both ? -1 : Flag.Conversions;
    }
}
