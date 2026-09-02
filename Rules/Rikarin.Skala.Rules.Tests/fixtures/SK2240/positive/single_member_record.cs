namespace Fixtures.SK2240;

public sealed record Wrapper(string Value);

public static class SingleMemberRecord {
    public static Wrapper Replace(Wrapper wrapper, string value) => wrapper with { Value = value };
}
