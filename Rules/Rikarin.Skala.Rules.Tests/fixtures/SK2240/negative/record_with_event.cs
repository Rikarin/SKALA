namespace Fixtures.SK2240;

public sealed record Watched(int X, int Y) {
    public event System.EventHandler? Changed;

    public void Raise() => Changed?.Invoke(this, System.EventArgs.Empty);
}

public static class RecordWithEvent {
    public static Watched Move(Watched value, int x, int y) => value with { X = x, Y = y };
}
