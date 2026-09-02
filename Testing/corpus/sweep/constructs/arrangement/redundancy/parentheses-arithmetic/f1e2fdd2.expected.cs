// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// dotnet_style_parentheses_in_arithmetic_binary_operators, alone on a file that carries no other
// category's case.
//
// ⚠ Alone on purpose, and the reason is the probe rather than the rule. `cleanupcode` re-resolves the
// three dotnet_style_parentheses_in_*_binary_operators keys from the section that mentions any one of
// them, so the key-flip sweep's appended `[*.cs]` override of a single key resets the other two to
// Roslyn's `always_for_clarity` defaults. Confirmed unbatched with `sweep verify` on
// redundancy/parentheses-categories.cs, which carries all three: flipping this key moved its own line
// correctly at *both* values and the *relational* line disagreed at both, so all three keys read
// DIVERGENT on a difference none of them caused. One category per file is what makes a one-key flip
// answer a one-key question; parentheses-categories.cs keeps the cross-category model and the shapes
// that have no key at all.
public class ParenthesesArithmetic {
    // arithmetic inside arithmetic: kept at always_for_clarity, removed at the export's
    // never_if_unnecessary.
    public int Arithmetic(int a, int b, int c) => a + (b * c);
}
