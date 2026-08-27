// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
using System;

namespace Skala.Corpus.Arrangement;

// resharper_prefer_explicit_discard_declaration. ⚠ The export writes `false`, at which value this
// file is arranged unchanged — the observable direction is the other one, and the option's coverage
// test is what exercises it. Measured: at `true` the oracle turns `out _` into `out var _`, and at
// `false` it does *not* do the reverse, because `false` means "do not add" rather than "remove".
public class DiscardDeclaration {
    public void Deconstruct(out int first, out int second) {
        first = 1;
        second = 2;
    }

    public void BareDiscard() {
        this.Deconstruct(out var kept, out _);
        Console.WriteLine(kept);
    }

    public void AlreadyExplicit() {
        this.Deconstruct(out var kept, out var _);
        Console.WriteLine(kept);
    }

    // ⚠ Not touched: a bare `_` that is not an `out` argument may be an ordinary variable, and
    // turning a read of it into a declaration would not compile.
    public int Ordinary() {
        var _ = 5;
        return _;
    }

    // ⚠ Not touched: a deconstruction's discard is a different position with a different key.
    public void Tuple() {
        var (first, _) = (1, 2);
        Console.WriteLine(first);
    }
}
