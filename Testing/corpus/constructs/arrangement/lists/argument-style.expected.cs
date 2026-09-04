// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
using System;

namespace Skala.Corpus.Arrangement;

// The four resharper_arguments_* keys, all `positional` in the export, so an argument name that is
// already in its own parameter's position is removed. ⚠ Four keys and not one: the argument's kind
// selects which key governs it, and a fixture that used a single kind would pin one and leave three
// unobservable.
//
// ⚠ `typeof(int)` and not `new object()` in the `other` position, and the reason is SK-DIV-0076
// rather than taste: an argument is a target-typed position the oracle converts to `new()` and Skala
// does not, so a `new T()` argument makes this file disagree with the oracle before any of the four
// keys is touched — and all four rows then attribute nothing. The construct is pinned on
// target-typed-new-argument.cs, which nothing is globbed to. `typeof(…)` falls in the same
// `arguments_other` bucket and no rule rewrites it.
public class ArgumentStyle {
    public void Take(int number, string text, Action callback, object other) { }

    // Every name here is redundant — each argument already sits at its own parameter.
    public void AllNamed() {
        Take(number: 1, text: "x", callback: () => { }, other: typeof(int));
    }

    // ⚠ Not touched: these names *reorder* the call. Dropping them would swap the operands, which is
    // why the rule compares each argument's name against the parameter at its own index rather than
    // just checking that the name exists.
    public void Reordered() {
        Take(text: "x", number: 1, other: typeof(int), callback: () => { });
    }

    // ⚠ Not touched: `out`/`ref`/`in` arguments keep their names, which are often the only thing
    // distinguishing one `out` parameter from the next.
    public void Directional() {
        Split(value: 3, first: out var a, second: out var b);
        Console.WriteLine(a + b);
    }

    static void Split(int value, out int first, out int second) {
        first = value;
        second = value;
    }

    // ⚠ Touched, and it used not to be: the rule declined every `params` call on the reasoning that
    // the positional mapping is a guess. Measured, it is not — the oracle strips `first:` and `rest:`
    // here exactly as it strips a name anywhere else, and only *adding* a name is restricted, to the
    // form that passes the array itself.
    public void Variadic() {
        Many(first: 1, rest: [2, 3]);
    }

    static void Many(int first, params int[] rest) { }
}
