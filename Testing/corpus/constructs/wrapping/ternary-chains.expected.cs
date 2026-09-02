// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
namespace Skala.Corpus.Wrapping;

// A *nested* conditional chain, which the oracle wraps by a rule of its own and not by the two keys
// whose names suggest it.
//
// ⚠ Measured against `jb cleanupcode` 2025.2.6 at the repository's 120-column margin. Flipping
// `wrap_ternary_expr_style` to `chop_always` or to `wrap_if_long`, and `wrap_before_ternary_opsigns`
// to `false`, returns every chain in this file byte-identical while it moves the single conditional
// in `NotAChain` beside them. The two layouts are therefore separate constructs:
//
//   a conditional whose tail is not a conditional  →  wraps at its own `?` and `:`
//   a chain of them                                →  wraps after each `:`, one member per line
//
// ⚠ The chain's members sit one continuation level from the *statement*, not one from whatever
// broke around them: `Argument` puts them on the argument's own column, because the parenthesis has
// already spent that level.
public class TernaryChains {
    public string Chopped(int flag) {
        // Chopped at both links and nowhere else. The innermost member keeps `? … : …` on the last
        // line, which is measured rather than assumed: a chain whose members are each wider than
        // the margin is still broken only at the links.
        var chain = flag > 10 ? "the first branch here" :
            flag > 5 ? "the second branch here" :
            flag > 1 ? "third" : "d";
        return chain;
    }

    public string Returned(int flag) {
        return flag > 10 ? "the first branch here it is" :
            flag > 5 ? "the second branch here" :
            flag > 1 ? "third" : "d";
    }

    public string Fits(int flag) {
        // Inside the margin, so nothing is broken at all.
        return flag > 10 ? "a" : flag > 5 ? "b" : flag > 1 ? "c" : "d";
    }

    public string KeptBroken(int flag) {
        // ⚠ Broken by the author and inside the margin, and the oracle keeps every one of those
        // breaks — the final else's included, which is not a break the formatter ever adds. A
        // single conditional written this way is re-joined instead; see constructs/breaks/ternary.cs.
        return flag > 10 ? "a" :
            flag > 5 ? "b" :
            "c";
    }

    public string KeptAtTheSigns(int flag) {
        // The other layout people write, and `keep_user_linebreaks` is what preserves it: at
        // `keep_user_linebreaks = false` the oracle rewrites this into the layout above.
        return flag > 10 ? "the first branch here"
            : flag > 5 ? "the second branch here"
            : flag > 1 ? "third" : "d";
    }

    public string KeptAtOneSignOnly(int flag) {
        // ⚠ The author broke exactly one sign of the chain and it was a `:`. The chain is planned
        // member by member, as above — but the members the author left flat still have to break,
        // and the measured answer is that they break at their `:` and never at their own `?`. The
        // line is over the margin, so the break is paid for by width.
        return flag > 10 ? "the first branch here"
            : flag > 5 ? "the second branch here"
            : flag > 1 ? "third"
            : "d";
    }

    public string KeptAtOneSignOnlyInsideTheMargin(int flag) {
        // ⚠ The same shape inside the margin, and it is the half that says the break is not about
        // width: the flat members break only because the member the author broke did. Both halves
        // are here because this is where idempotence lives — a break before a `?` is what turns the
        // staircase on (see BreakPlan.PlanTernary), and it is read off the source, so a pass that
        // broke these members at both signs would hand the next pass a chain it steps. It did, at
        // 4, 8 and 12, and pass 3 agreed with pass 2.
        return flag > 10 ? "a"
            : flag > 5 ? "b"
            : flag > 1 ? "c"
            : "d";
    }

    public void Argument(int flag) {
        Use(
            flag > 10 ? "the first branch here it is now" :
            flag > 5 ? "the second branch here" :
            flag > 1 ? "third" : "d"
        );
    }

    public string NotAChain(int flag) {
        // The tail is not a conditional, so this is one conditional and it wraps at its signs.
        return flag > 10 ? "the first value is larger than the second one here and then some" : "the second value";
    }

    public string NestedOnTheTrueSide(int flag) {
        // ⚠ Nesting on the *true* side is not a chain either. The chain runs through `WhenFalse`
        // and nowhere else, and the oracle lays this out at the outer conditional's signs.
        return flag > 10
            ? flag > 100 ? "the innermost branch here" : "the middle branch here"
            : "the outer branch here";
    }

    public string ParenthesisedTail(int flag) {
        // ⚠ And the chain does not see through parentheses: with the tail bracketed the oracle goes
        // back to the single conditional's layout.
        return flag > 10
            ? "the first branch here and more"
            : (flag > 5 ? "the second branch here" : flag > 1 ? "t" : "d");
    }

    static void Use(string value) { }
}
