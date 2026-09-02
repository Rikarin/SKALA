// analyzer-option: dotnet_code_quality.SK6034.frozen_constant_types = ProtocolVersions
// ⚠ Anti-vacuity for the negative beside it: the key set to *some* value must not switch the rule
// off. `Limits` is not on the list, so its constant is still reported.

public static class Limits {
    public const int MaxRetries = 3;
}
