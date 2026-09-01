// analyzer-option: dotnet_code_quality.SK7083.threshold = 2
// ⚠ The comparison is on the literal's *value*, not on how it was spelled. A verbatim string, a
// regular one and a raw one that all say the same thing are the same rename, so they count together.
namespace Fixtures;

class Spellings {
    public const string Regular = "artifacts";

    public static string One() => "artifacts";

    public static string Two() => @"artifacts";

    public static string Three() => """artifacts""";
}
