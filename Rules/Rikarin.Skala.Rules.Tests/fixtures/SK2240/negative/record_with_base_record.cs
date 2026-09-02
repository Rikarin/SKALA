namespace Fixtures.SK2240;

public record Origin(int X);

public sealed record Offset(int X, int Y) : Origin(X);

public static class RecordWithBaseRecord {
    public static Offset Move(Offset value, int x, int y) => value with { X = x, Y = y };
}
