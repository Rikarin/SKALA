// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaCleanup generated=2026-08-27
namespace Skala.Corpus.Arrangement;

// ⚠ SK-DIV-0013: the oracle performs none of this. The fixture beside it is the format-only output
// twice over, and the rewrite is pinned by ArrangementRuleTests instead. What the file is for is the
// SAFETY case: `!= null` and `is not null` are different expressions when the operand's type
// declares operator ==, and Skala must refuse the rewrite there.
public class HasEqualityOperator {
    public static bool operator ==(HasEqualityOperator left, HasEqualityOperator right) => ReferenceEquals(left, right);

    public static bool operator !=(HasEqualityOperator left, HasEqualityOperator right) =>
        !ReferenceEquals(left, right);

    public override bool Equals(object obj) => ReferenceEquals(this, obj);

    public override int GetHashCode() => 0;
}

public class InheritsEqualityOperator : HasEqualityOperator { }

public class Plain {
    public int Value;
}

public class NullCheckingPattern {
    // Rewritten: no user-defined operator ==.
    public void Safe(Plain p, string s, int? n) {
        if (p != null) {
            Console.WriteLine("p");
        }

        if (s == null) {
            Console.WriteLine("s");
        }

        if (n != null) {
            Console.WriteLine("n");
        }

        if (null != p) {
            Console.WriteLine("reversed");
        }
    }

    // ⚠ Refused: the operator form calls the user's operator, the pattern form does not.
    public void Unsafe(HasEqualityOperator a, InheritsEqualityOperator b) {
        if (a != null) {
            Console.WriteLine("a");
        }

        if (b != null) {
            Console.WriteLine("b, through the base class");
        }
    }
}
