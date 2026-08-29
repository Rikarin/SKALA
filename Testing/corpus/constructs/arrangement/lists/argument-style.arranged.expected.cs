// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaCleanup generated=2026-08-29
namespace Skala.Corpus.Arrangement;

// The four resharper_arguments_* keys, all `positional` in the export, so an argument name that is
// already in its own parameter's position is removed. ⚠ Four keys and not one: the argument's kind
// selects which key governs it, and a fixture that used a single kind would pin one and leave three
// unobservable.
public class ArgumentStyle {
    public void Take(int number, string text, Action callback, object other) { }

    // Every name here is redundant — each argument already sits at its own parameter.
    public void AllNamed() {
        Take(1, "x", () => { }, new());
    }

    // ⚠ Not touched: these names *reorder* the call. Dropping them would swap the operands, which is
    // why the rule compares each argument's name against the parameter at its own index rather than
    // just checking that the name exists.
    public void Reordered() {
        Take(text: "x", number: 1, other: new(), callback: () => { });
    }

    // ⚠ Not touched: `out`/`ref`/`in` arguments keep their names, which are often the only thing
    // distinguishing one `out` parameter from the next.
    public void Directional() {
        Split(3, out var a, out var b);
        Console.WriteLine(a + b);
    }

    static void Split(int value, out int first, out int second) {
        first = value;
        second = value;
    }

    // ⚠ Not touched: a `params` call has no reliable positional mapping, so the rule declines it.
    public void Variadic() {
        Many(1, [2, 3]);
    }

    static void Many(int first, params int[] rest) { }
}
