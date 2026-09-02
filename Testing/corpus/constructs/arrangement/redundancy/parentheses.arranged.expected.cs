// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// resharper_parentheses_redundancy_style = remove_if_not_clarifies_precedence, against
// dotnet_style_parentheses_in_arithmetic_binary_operators = never_if_unnecessary,
// ..._relational_binary_operators = never_if_unnecessary and
// ..._other_binary_operators = always_for_clarity, with
// resharper_parentheses_non_obvious_operations naming shift and the bitwise family.
//
// ⚠ The deciding factor is the *inner* operation's kind and not the parent's. Every "kept" case
// below sits in the same syntactic position as a "removed" one.
public class Parentheses {
    // Removed: the inner binds tighter and the family is arithmetic.
    public int Arithmetic(int a, int b, int c) => a + b * c;

    // Removed: relational is never_if_unnecessary, even as an operand of `&&`.
    public bool Relational(int a, int b, int c) => a < b && b < c;

    // Kept: `&&` and `||` are "other binary operators", always_for_clarity.
    public bool Logical(bool a, bool b, bool c) => a || (b && c);

    // Kept: the bitwise family is a non-obvious operation.
    public int BitwiseAnd(int a, int b, int c) => a | (b & c);

    public int ExclusiveOr(int a, int b, int c) => a | (b ^ c);

    // Kept: shift is a non-obvious operation.
    public int Shift(int a, int b, int c) => a + (b << c);

    // Kept: null-coalescing is "other".
    public string Coalesce(string a, string b, string c) => a ?? (b ?? c);

    // Removed: nested parentheses are redundant whatever they wrap.
    public int Nested(int a) => a;

    // Removed: an initializer takes any expression.
    public int Initializer(int a, int b) {
        var x = a + b;
        return x;
    }

    // Removed: a cast binds tighter than `+`.
    public int Cast(object o) => (int)o + 1;

    // Removed: unary operators bind tighter than any binary one.
    public int Unary(int a, int b) => -a + b;

    public bool Not(bool a, bool b) => !a && b;

    // Removed: an invocation is primary.
    public int Invocation(int a) => Arithmetic(a, a, a) + 1;

    // ⚠ Kept: an assignment inside an expression is doing work the reader is being shown.
    public int Assignment(int a) {
        var x = 0;
        var y = (x = a) + 1;
        return y;
    }

    // ⚠ Kept: `(a ? b : c)` reads as deliberate everywhere it appears in the corpus.
    public int Conditional(bool a, int b, int c) => (a ? b : c) + 1;

    // Removed: the oracle drops these, and the re-parse proof agrees the pattern variable is still
    // in scope afterwards. This one was written down as "kept" from intuition and measured otherwise.
    public bool Pattern(object o) => o is string s && s.Length > 0;

    // ⚠ Kept: the parser cannot tell `(A)(b)` — a cast — from an invocation without semantics, so
    // the operand of a cast is declined whole.
    public int CastOperand(int a) => (int)(a + 1);

    // ⚠ Kept: the *enclosing* operation is a non-obvious one, so its operands keep their
    // parentheses even though the operand itself is plain arithmetic. This is the half of
    // `parentheses_non_obvious_operations` that reads as being about the inner expression and is
    // not — got wrong first, then measured.
    public int ArithmeticInsideBitwise(int a, int b) => a & (b + 1);

    public int ArithmeticInsideShift(int a, int b) => a << (b + 1);

    public uint Mask(uint value, int offset, int take) => (value >> offset) & ((1 << take) - 1);

    // Kept: the inner is a shift, so it keeps its own wherever it sits.
    public int ShiftInsideArithmetic(int a, int b, int c) => (a << b) + c;

    public int BitwiseInsideArithmetic(int a, int b, int c) => (a & b) + c;

    // Removed: both sides are arithmetic.
    public int Length(byte[] buffer, int bits) => buffer.Length * 8 - bits;

    // ⚠ Not touched: subtraction and division are not associative, and the proof refuses them
    // because the re-parse is not equivalent.
    public int NotAssociative(int a, int b, int c) => a - (b - c);

    public int Division(int a, int b, int c) => a / (b / c);
}
