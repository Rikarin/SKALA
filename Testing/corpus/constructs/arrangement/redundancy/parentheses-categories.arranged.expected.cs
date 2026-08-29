// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaCleanup generated=2026-08-29
namespace Skala.Corpus.Arrangement;

// The three dotnet_style_parentheses_in_*_binary_operators keys, one case each, plus the two shapes
// that have no key at all.
//
// ⚠ Each key governs only the parentheses whose *parent* binary operator is of the same precedence
// kind as the parenthesised expression's own. Measured against jb cleanupcode 2025.2.6 under the
// cleanup profile with all three keys restated in a trailing [*.cs] section — restating one alone
// resets the other two to Roslyn's defaults, so a single-key probe silently measures three. At the
// export's values the restatement reproduces this file byte for byte, and flipping one key moves
// exactly one of the first three lines below.
//
// ⚠ redundancy/parentheses.cs is the companion, and it covers the families these keys do not: shift
// and bitwise are resharper_parentheses_non_obvious_operations and hold at all eight combinations.
public class ParenthesesCategories {
    // arithmetic in arithmetic: never_if_unnecessary, so removed. Kept at always_for_clarity.
    public int Arithmetic(int a, int b, int c) => a + b * c;

    // relational in relational: never_if_unnecessary, so removed. Kept at always_for_clarity.
    public bool Relational(int a, int b, int c, int d) => a > b == c > d;

    // other in other: always_for_clarity, so kept. Removed at never_if_unnecessary.
    public bool Other(bool a, bool b, bool c) => a || (b && c);

    // ⚠ No key: the parent is relational and the operand is arithmetic, so the two kinds differ and
    // the parentheses go at every combination of the three.
    public bool Mixed(int a, int b, int c) => a + b > c;

    // ⚠ No key either: the parent is not a binary operator at all. This is the case the first
    // version of the rule got wrong — keying on the operand alone keeps `(a && b)` here because
    // `&&` is always_for_clarity, and the oracle removes it.
    public bool NoBinaryParent(bool a, bool b) => a && b;

    public int NoBinaryParentArithmetic(int a, int b) => a + b;
}
