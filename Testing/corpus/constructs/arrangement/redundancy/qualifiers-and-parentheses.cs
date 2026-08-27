using System;

namespace Skala.Corpus.Arrangement;

// this-qualifier removal, predefined type names, redundant nested braces, and the parenthesis
// removal that is gated behind --aggressive.
public class QualifiersAndParentheses {
    private int _count;
    private static int _shared;

    public void ThisQualifier() {
        this._count = 1;
        Console.WriteLine(this._count);
        this.Helper();
    }

    // ⚠ Refused: a parameter shadows the field, so the bare name would bind to the parameter.
    public void Shadowed(int _count) {
        this._count = _count;
    }

    public void PredefinedTypes() {
        Int32 a = 1;
        String b = "x";
        Boolean c = true;
        Object d = null;

        // ⚠ builtin_type_apply_to_native_integer = false, so these stay as written.
        IntPtr handle = IntPtr.Zero;
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
            int scoped = a;
            Console.WriteLine(scoped);
        }
    }

    public void Parentheses(int a, int b, int c) {
        // --aggressive only. Precedence alone settles these.
        int x = a + (b * c);
        int y = (a + b) + c;

        // Never removed: the right-hand side of a non-associative operator changes meaning.
        int p = a - (b - c);
        int q = a / (b / c);

        // Never removed: other_binary_operators = always_for_clarity.
        bool r = (a > b) && (b > c);
        Console.WriteLine(x + y + p + q + (r ? 1 : 0));
    }

    private void Helper() {
        _shared = 1;
    }
}
