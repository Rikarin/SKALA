// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-28
namespace Skala.Corpus.Wrapping;

// `align_tuple_components`, and the break point it governs.
//
// The key was filed as unimplementable twice, and neither reason survived a tuple long enough to
// wrap. The first was "no probe found a shape where it changes the oracle's output" — the probes
// used tuples that fit. The second was the break: Skala aligned a wrapped tuple correctly and never
// wrapped one, breaking after the `=` instead and leaving the components flat, so there was no line
// for a column to govern.
//
// The key is false in the export, so this file is the level-indented shape — one continuation indent
// from the statement — and the option unit is what moves it onto the `(`'s column:
//
//     var aligned = (FirstComponentName: 1, SecondComponentName: 2, …
//                    FifthComponentName: 5);      ← at `align_tuple_components = true`
public class TupleComponents {
    // ⚠ Wraps under the export's own 120-column margin. A shape that only wraps when a second key is
    // flipped is one the per-option unit, which flips exactly one key, can never reach.
    public void Wraps() {
        var wrapped = (FirstComponentName: 1, SecondComponentName: 2, AThirdComponentName: 3, FourthComponentName: 4,
            FifthComponentName: 5);
        System.Console.WriteLine(wrapped);
    }

    // ⚠ A tuple is a *fill* and has no wrap-style key of its own. `wrap_arguments_style = chop_always`
    // leaves this one exactly as it is: the components run to the margin and the remainder takes the
    // next line, rather than one component per line.
    public void FillsRatherThanChops() {
        var filled = (FirstComponentName: 1, SecondComponentName: 2, AThirdComponentName: 3, FourthComponentName: 4,
            FifthComponentName: 5, SixthComponentName: 6, SeventhComponentName: 7);
        System.Console.WriteLine(filled);
    }

    // ⚠ Neither delimiter is a break point. Even a tuple too wide for the continuation line keeps the
    // `(` on the statement's line and the `)` on the last component's.
    public void KeepsItsParentheses() {
        var kept = (FirstComponentNameThatIsLong: 1, SecondComponentNameThatIsLong: 2, AThirdComponentNameLong: 3,
            FourthComponentNameLong: 4, FifthComponentNameThatIsLong: 5, SixthComponentNameIsLong: 6);
        System.Console.WriteLine(kept);
    }

    // ⚠ A break the author wrote between two components is kept where it is, although the whole tuple
    // would fit on one line — a tuple pins its item breaks where an array initializer re-fills its.
    // The tail is still filled: see the second break the oracle adds below and not above.
    public void KeepsTheAuthorsBreaks() {
        var pinned = (FirstComponentName: 1,
            SecondComponentName: 2, AThirdComponentName: 3, FourthComponentName: 4, FifthComponentName: 5,
            SixthComponentName: 6);
        System.Console.WriteLine(pinned);
    }

    // ⚠ A tuple that fits is not touched at either value: there is no break for a column to govern,
    // and the key is about where a break lands rather than about whether one happens.
    public void Fits() {
        var short_ = (First: 1, Second: 2);
        System.Console.WriteLine(short_);
    }
}
