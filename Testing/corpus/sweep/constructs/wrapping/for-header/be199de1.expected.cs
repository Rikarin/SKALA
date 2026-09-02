// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
namespace Skala.Corpus.Wrapping;

// `wrap_for_stmt_header_style = chop_if_long`, and the break point at the header's `;`.
//
// Recorded as reached-but-unimplementable until milestone 3.2, and the reason was true rather than
// final: Skala had no break point at the `;` at all, so a `for` header that did not fit came back
// broken inside the incrementor expression — `i +=\n 1` — instead of one clause per line.
//
// The clauses land on the column after the `(`. That column is not this key's: it is
// `align_multiline_statement_conditions = true`, which the export sets and which already governed
// every other statement header. `align_multiline_for_stmt` is masked by it at the export's values —
// both of its values produce this file byte for byte — which is why that key is Tier D and this one
// is not.
public class ForHeader {
    // ⚠ Wraps under the export's own 120-column margin, so the per-option unit can reach it with one
    // flip. At `wrap_if_long` the initializer and the condition share the first line and only the
    // incrementor moves; at `chop_always` even the short header below is chopped.
    public void Chops(System.Collections.Generic.List<int> someCollectionOfThings) {
        for (var indexOfTheOuterLoop = 0;
             indexOfTheOuterLoop < someCollectionOfThings.Count;
             indexOfTheOuterLoop += 1) {
            System.Console.WriteLine(indexOfTheOuterLoop);
        }
    }

    // ⚠ "Chop if long *or multiline*": a header the author broke at one `;` comes back with both
    // gaps broken, although it fits on one line. At `wrap_if_long` only the gap the author broke is
    // kept, which is the one shape that tells a fill from a chop on already-broken input.
    public void ChopsWhatTheAuthorBrokeOnce() {
        for (var indexOfTheOuterLoop = 0;
             indexOfTheOuterLoop < 10;
             indexOfTheOuterLoop++) {
            System.Console.WriteLine(indexOfTheOuterLoop);
        }
    }

    // ⚠ "Multiline" is any break inside the parentheses and not only one at a `;`, and the shape came
    // out of corpus/real/ rather than out of a guess: a header the author broke inside its *condition*
    // comes back from `chop_if_long` with the semicolons broken too, and from `wrap_if_long`
    // untouched. It also shows that an empty initializer beside a full condition still puts `for (;`
    // on a line of its own — the point is before each clause that exists, not after each semicolon.
    public void ChopsOnABreakInsideAClause(System.IO.Stream s1, System.IO.Stream s2) {
        for (;
             ((s1.Position != s1.Length)
                 && (s1.ReadByte() == s2.ReadByte()));) {
            System.Console.WriteLine(s1);
        }
    }

    // ⚠ A clause that is empty is not a break point — a line holding nothing is not a layout.
    public void EmptyClauses() {
        var indexOfTheOuterLoop = 0;
        for (;
             indexOfTheOuterLoop < 10;) {
            indexOfTheOuterLoop++;
        }
    }

    // ⚠ A header that fits is left alone at `chop_if_long` and chopped at `chop_always`.
    public void Fits() {
        for (var i = 0;
             i < 10;
             i++) {
            System.Console.WriteLine(i);
        }
    }
}
