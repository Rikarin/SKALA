// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// this-qualifier removal, predefined type names, and the parenthesis removal that was gated behind
// --aggressive.
//
// ⚠ The redundant-brace case moved to braces-redundant.cs, and it is not a tidy-up. Removing a
// redundant brace pair is SK-DIV-0013: the export configures it, Skala performs it and
// `jb cleanupcode` does not, so a file holding one can never agree with the oracle at the sweep's
// baseline — and while it sat here it took `dotnet_style_predefined_type_for_locals_parameters_members`
// and `dotnet_style_require_accessibility_modifiers` down with it, two keys with nothing wrong with
// them whose rows attributed nothing as a result. The divergence is pinned on its own file now, so
// only the key that owns it pays for it.
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
