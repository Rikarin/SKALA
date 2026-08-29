// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaCleanup generated=2026-08-29
namespace Skala.Corpus.Arrangement;

// this-qualifier removal, predefined type names, redundant nested braces, and the parenthesis
// removal that is gated behind --aggressive.
public class QualifiersAndParentheses {
    int _count;
    static int _shared;

    public void ThisQualifier() {
        _count = 1;
        Console.WriteLine(_count);
        Helper();
    }

    // ⚠ Refused: a parameter shadows the field, so the bare name would bind to the parameter.
    public void Shadowed(int _count) {
        this._count = _count;
    }

    public void PredefinedTypes() {
        var a = 1;
        var b = "x";
        var c = true;
        object d = null;

        // ⚠ builtin_type_apply_to_native_integer = false, so these stay as written.
        var handle = IntPtr.Zero;
        Console.WriteLine(nameof(Int32));
    }

    public void RedundantBraces(int a) {
        {
            {
                Console.WriteLine(a);
            }
        }

        // ⚠ Refused: the inner block declares, and lifting it widens the declaration's scope.
        {
            var scoped = a;
            Console.WriteLine(scoped);
        }
    }

    public void Parentheses(int a, int b, int c) {
        // --aggressive only. Precedence alone settles these.
        var x = a + b * c;
        var y = a + b + c;

        // Never removed: the right-hand side of a non-associative operator changes meaning.
        var p = a - (b - c);
        var q = a / (b / c);

        // Never removed: other_binary_operators = always_for_clarity.
        var r = a > b && b > c;
        Console.WriteLine(x + y + p + q + (r ? 1 : 0));
    }

    void Helper() {
        _shared = 1;
    }
}
