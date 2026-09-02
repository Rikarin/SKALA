// ⚠ #330: `public static readonly int Ok = 0;` is `CA1805` — an explicit default initialiser is
// redundant on a field and legitimate only on a `const` — so the fix drops the initialiser with the
// keyword rather than trading this rule's finding for the SDK's.

public static class Codes {
    public const int Ok = 0;
}
