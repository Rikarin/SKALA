using System.Text.RegularExpressions;

// ⚠ Serilog's `SettingValueConversions.StaticMemberAccessorRegex`, verbatim. The `+` and the `*` are
// inside the groups, applying to the classes; no group is itself repeated, so nothing nests.
public static class Conversions {
    static readonly Regex StaticMemberAccessor =
        new(@"^(?<shortTypeName>[^:]+)::(?<memberName>[A-Za-z][A-Za-z0-9]*)(?<extraQualifiers>[^:]*)$");

    public static bool IsAccessor(string input) => StaticMemberAccessor.IsMatch(input);
}
