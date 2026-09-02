namespace Fixtures.SK2240;

public sealed record Vector(double X, double Y, double Z);

public static class ThreeMemberRecord {
    public static Vector Reset(Vector vector, double x, double y, double z) =>
        vector with { X = x, Y = y, Z = z };
}
