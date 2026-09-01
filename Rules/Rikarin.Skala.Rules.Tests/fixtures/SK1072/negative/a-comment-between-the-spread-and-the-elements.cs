public sealed class Codes {
    public int[] All(int fallback) => [
        .. /* the two the protocol names */ new[] { 200, 204 },
        fallback
    ];
}
