// analyzer-option: dotnet_code_quality.SK7083.threshold = 1
// ⚠ The floor is what makes this rule usable. Even at a threshold of one, a four-character word
// repeated six times says nothing a name would say better — and without the floor the rule reports
// every flag and every short key in the repository before it reports anything worth extracting.
namespace Fixtures;

class Flags {
    public static readonly string A = "true";

    public static readonly string B = "true";

    public static readonly string C = "true";

    public static readonly string D = "true";

    public static readonly string E = "true";

    public static readonly string F = "true";
}
