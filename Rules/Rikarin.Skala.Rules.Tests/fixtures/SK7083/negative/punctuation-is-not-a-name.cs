// analyzer-option: dotnet_code_quality.SK7083.threshold = 1
// ⚠ The letter test is not a refinement of the length test. Every literal here clears any length
// floor anybody would set, and none of them is a name waiting to be given: what makes a repeated
// literal worth extracting is that it *says* something, and in C# that means it has letters in it.
namespace Fixtures;

class Separators {
    public static readonly string Rule = "----------";

    public static readonly string Rule2 = "----------";

    public static readonly string Rule3 = "----------";

    public static readonly string Rule4 = "----------";

    public static readonly string Bar = " | | | ";

    public static readonly string Bar2 = " | | | ";

    public static readonly string Bar3 = " | | | ";

    public static readonly string Bar4 = " | | | ";
}
