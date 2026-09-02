// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
using System.Collections.Generic;
using System.Linq;

class ChainedCalls {
    void Fits(IEnumerable<string> source) {
        var a = source.Where(x => x.Length > 3).Select(x => x);
    }

    void DoesNotFit(IEnumerable<string> source) {
        var b = source.Where(x => x.Length > 3)
            .OrderBy(x => x)
            .Select(x => x.ToUpperInvariant())
            .ToList()
            .AsReadOnly()
            .Count();
    }

    void PropertyInTheChain(IEnumerable<string> source) {
        var c = source.Where(x => x.Length > 3)
            .ToList()
            .Count.ToString()
            .Trim()
            .Substring(0, 1)
            .ToUpperInvariant()
            .Trim();
    }

    // ⚠ SK-DIV-0030. A chain whose *root* is the `?.` — the whole thing is one
    // `ConditionalAccessExpressionSyntax` — is the shape the chain planner could not see: the `?` is
    // that node's own operator token and every dot of the chain, `.Where`'s included, hangs off
    // `WhenNotNull`, which the receiver-side walk never reached. `dots` came back empty, no group was
    // planned, and the chain had no break points at all, so the *argument list* of the last call took
    // the break and left a dangling `)`.
    //
    // ⚠ The receiver is `sourceOfStringsHere` and not `source` on purpose: at `source` the whole
    // chain is 116 columns, the oracle leaves it flat, and the fixture pins nothing at all — the
    // first draft of this method did exactly that and the regenerated `.expected.cs` came back
    // unwrapped. It is 129 columns now, so both engines are being asked the question.
    void RootedAtAConditionalAccess(IEnumerable<string>? sourceOfStringsHere) {
        var d = sourceOfStringsHere?.Where(x => x.Length > 3)
            .OrderBy(x => x)
            .Select(x => x.ToUpperInvariant())
            .ToList()
            .Count();
    }

    // ⚠ The control, and the reason the method above is attributable: the same chain with `.` for
    // `?.` was chopped correctly before the fix and after it. Whatever the pair disagrees about, it
    // is not the chain planner in general.
    void TheSameChainWithoutTheConditional(IEnumerable<string> sourceOfStringsHere) {
        var e = sourceOfStringsHere.Where(x => x.Length > 3)
            .OrderBy(x => x)
            .Select(x => x.ToUpperInvariant())
            .ToList()
            .Count();
    }

    // The chain root's *context* varies where the continuation levels come from, so the shape is
    // pinned in three of them rather than one. Expression-bodied: the arrow has already broken, and
    // the chain takes its own level on top of that — two levels, not one.
    //
    // ⚠ Long enough that it still does not fit *after* the arrow break. The first draft fitted at
    // indent 8 once the arrow had broken, so the oracle left the chain flat and the method pinned
    // the arrow rather than the chain.
    int ExpressionBodied(IEnumerable<string>? sourceOfStringsHere) =>
        sourceOfStringsHere?.Where(x => x.Length > 3)
            .OrderByDescending(x => x)
            .Select(x => x.ToUpperInvariant())
            .ToList()
            .Count()
        ?? 0;

    // The `?.`'s own receiver is a chain rather than a bare identifier, so the receiver-side walk has
    // something to do as well as the `WhenNotNull` side — and it is the case where the `?` becomes a
    // break point rather than being held back as the chain's first dot. This is the shape that says
    // the point is the `?` and not the `.` beside it; with a bare identifier receiver the binding's
    // dot is the last entry in the list and `wrap_before_first_method_call = false` never spends it.
    //
    // ⚠ The receiver is `.Concat(…)` and not `this.sourceOfStringsHere`, and the difference is not
    // cosmetic. A *property* receiver puts a property run either side of the `?`, which the run loop
    // still cuts at the `?` (SK-DIV-0067 shape C) — and the draft that used one took
    // `resharper_csharp_wrap_before_first_method_call` from Conformant to Divergent at its other
    // value, demoting a Tier A key on the strength of an open defect this file is not about.
    void TheReceiverIsItselfAChain(IEnumerable<string> sourceOfStringsHere, IEnumerable<string> more) {
        var f = sourceOfStringsHere.Concat(more)
            ?.Where(x => x.Length > 3)
            .OrderBy(x => x)
            .Select(x => x.Trim())
            .ToList()
            .Count();
    }

    // Already broken by the author. Without a group there is nothing for
    // `keep_existing_arrangement`'s "the author broke this gap" fact to attach to, so the chain was
    // rejoined; with one it is kept.
    void AlreadyBrokenByTheAuthor(IEnumerable<string>? sourceOfStringsHere) {
        var g = sourceOfStringsHere?.Where(x => x.Length > 3)
            .OrderBy(x => x)
            .Select(x => x.ToUpperInvariant())
            .ToList()
            .Count();
    }

    // ⚠ The negative control. A `?.` chain that fits is left flat by both engines, and always was —
    // the defect needed a chain over the margin, which is why nothing in `corpus/real/` could see
    // it: that set has 102 files containing `?.method(` and not one where a `?.` call is followed by
    // another call, so `dots.Count < 2` returned early on every one of them.
    void ShortEnoughToStayFlat(IEnumerable<string>? source) {
        var h = source?.Where(x => x.Length > 3).ToList();
    }
}
