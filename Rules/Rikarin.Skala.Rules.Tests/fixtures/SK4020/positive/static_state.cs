using System;

static class ParenthesizedFixture {
    const string Marker = "marker";

    public static Func<string> Describe() => () => Marker + nameof(Describe);
}
