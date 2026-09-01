// analyzer-option: dotnet_code_quality.SK7082.threshold = 2
// The boundary, from both sides. `Flat` is one level and `Two` is exactly two, and the family
// reports `> threshold`, so a threshold of two leaves both silent. `nested-in-the-true-branch`
// is the same shape as `Two` and fires at the default threshold of one.
namespace Fixtures;

class Boundary {
    public static string Flat(bool a) => a ? "yes" : "no";

    public static string Two(bool a, bool b) => a ? b ? "both" : "first" : "neither";
}
