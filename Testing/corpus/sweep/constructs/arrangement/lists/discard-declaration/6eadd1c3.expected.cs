// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaCleanup generated=2026-09-02
namespace Skala.Corpus.Arrangement;

// resharper_prefer_explicit_discard_declaration, and both of its values move this file.
//
// ⚠ The comment here used to say that at the export's `false` the file is arranged unchanged, because
// "`false` means do not add rather than remove". Re-measured unbatched under the cleanup profile, that
// is wrong, and wrong in a way that cost this file its sweep baseline:
//   written        false (the export)   true
//   out _          out _                out var _
//   out var _      out _                out var _
//   out int _      out var _            out var _
// The claim was read off the third row, where this key declines a *typed* declaration and the `var`
// rule converts it afterwards — a fact about that shape and not about the key. The second row is the
// key's own answer, and at `false` it removes.
public class DiscardDeclaration {
    public void Deconstruct(out int first, out int second) {
        first = 1;
        second = 2;
    }

    public void BareDiscard() {
        Deconstruct(out var kept, out var _);
        Console.WriteLine(kept);
    }

    public void AlreadyExplicit() {
        Deconstruct(out var kept, out var _);
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
