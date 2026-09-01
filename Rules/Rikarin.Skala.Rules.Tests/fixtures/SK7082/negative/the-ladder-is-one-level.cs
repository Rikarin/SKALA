// ⚠ The exemption that decides whether this rule is usable. A right-associated chain is an
// `else if` ladder written as an expression: it reads top to bottom, each condition is tested once,
// and nobody counts brackets to follow one. Four rungs, one level, silent at the default threshold.
namespace Fixtures;

class Sizes {
    public static string Describe(int n) =>
        n < 10 ? "tiny"
        : n < 100 ? "small"
        : n < 1000 ? "medium"
        : "large";
}
