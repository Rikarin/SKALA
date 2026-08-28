// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
using System;

namespace Skala.Corpus.Arrangement;

// docs/plan/06's five conditions for use_heuristics_for_body_style, one member each — and the sixth
// case, the long one, which the doc said would be refused and the oracle converts.
public class Heuristics {
    private int _n;

    public int SingleReturn() {
        return 1;
    }

    public void SingleExpression() {
        Console.WriteLine("x");
    }

    // ⚠ Converted, at 190 columns. Doc 06's condition (c) — "fits max_line_length" — is not a
    // condition the oracle applies; it converts and the reformat wraps after the `=>`.
    public string Long(string aaaaaaaaaaaaaaaa, string bbbbbbbbbbbbbbbb, string cccccccccccccccc) {
        return aaaaaaaaaaaaaaaa
            + bbbbbbbbbbbbbbbb
            + cccccccccccccccc
            + aaaaaaaaaaaaaaaa
            + bbbbbbbbbbbbbbbb
            + cccccccccccccccc;
    }

    // (a) a throw in statement position stays a block.
    public void Throws(string s) {
        throw new ArgumentNullException(nameof(s));
    }

    // (b) a comment inside the body stays a block: there is nowhere for the comment to go.
    public void Commented() {
        // the author said something here
        Console.WriteLine("x");
    }

    // (d) async void is never converted.
    public async void AsyncVoid() {
        await System.Threading.Tasks.Task.Delay(1);
    }

    // (e) a #if inside the member stays a block.
    public void Conditional() {
#if DEBUG
        Console.WriteLine("debug");
#endif
    }

    // More than one statement is not a candidate at all.
    public void Two() {
        Console.WriteLine("a");
        Console.WriteLine("b");
    }

    // A bare `return;` has no expression to become one.
    public void BareReturn() {
        return;
    }

    // constructor_or_destructor_body = block_body, so this stays a block.
    public Heuristics() {
        _n = 1;
    }

    public int Read() {
        return _n;
    }
}
