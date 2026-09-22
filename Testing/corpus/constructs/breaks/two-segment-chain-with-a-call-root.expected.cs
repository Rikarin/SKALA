// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-22
namespace Constructs.Breaks;

// Issue #380, SK-DIV-0128 and SK-DIV-0129. A chain is counted in calls, not in dots, and its first
// call need not have a dot: `SomeMethod(…).Other(…)` is a chain of two calls, so the one dot it has is
// a break point and the chain breaks there rather than chopping `.Other`'s arguments. The same holds
// for a generic `F<T>(…)`, a delegate invocation `handler(a)(b)`, an element access `arr[0]` or
// `x.Items[0]` at the head, and for the shapes after the head — `[0]`, `?.`, `.Prop.Other` — that an
// identifier-headed chain already handled. `x.Other(…)`, `new T(…).Other(…)`, `(F(…)).Other(…)` and
// `x?.Other(…)` are not chains of two calls and chop the last argument list, as before. Before this
// file every call-headed two-segment chain chopped `.Other`'s arguments over four lines.
//
// Not here, deliberately: a chopped root argument list under a chain that breaks (the oracle spends
// the chain's level over it, `SomeMethod(` / args at 16 / `)` at 12 / `.Other(d)` at 12, and Skala
// puts the arguments at 12 and the `)` at 8) is SK-DIV-0112's open half and is the same for a property
// root; a chain ending in a property is SK-DIV-0066; a chain that is the operand of `??` is
// SK-DIV-0068's third item.
public class TwoSegmentChainWithACallRoot {
    void Statements() {
        var a3 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var a4 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(
                ddddddddddddddddddddddddddddddddddd,
                eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee,
                fffffffffffffffffffffffffffffffffffffffffffffff,
                gggggggggggggggggggg
            );
        var a7 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb)
            .Other(ddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeee)
            .Third(ffffffffffffffffffff, gggggggggggggg);
        var a8 = SomeMethod<TTTTTTTTTTT>(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var b1 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)[0]
            .Other(ddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var b2 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            ?.Other(ddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var b3 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Prop.Other(ddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var b6 = await SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var b7 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, x => x.Value);
        var c5 = handler(aaaaaaaaaaaaaaaaaa)(bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var c8 = SomeMethod(aaaaaaaaaaaaaaaaaa, x => x.bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var c9 = SomeMethodWithALongNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee()
            .Other()
            .Third()
            .Fourth()
            .Fifth()
            .Sixth();
        var d5 = SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb)
            .Other(ccccccccccc)
            .Third(
                ddddddddddddddddddddddddddddddddddd,
                eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee,
                ffffffffffffffffffffffffffffffffffff
            );
        SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc, gggggggggg)
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        Consume(
            SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
                .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff)
        );
        return SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc, ggggg)
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
    }

    object ExpressionBodied() =>
        SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc, gggggggggg)
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);

    void ElementAccessHeads() {
        var c3 = arrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr[0]
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var c4 = arrrrrrrrrrrrrrrrrrrrrrr[0]
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff)
            .Third(ggggggggggggggggggggggggg);
        var d1 = xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.Items[0]
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var d2 = xxxxxxxxxxxxxxxxxxxx.Items[0]
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff)
            .Third(gggggggggggggggggggggggggg);
        var d3 = arrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr[0][1]
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
    }

    void HeadsThatAreNotCalls() {
        var i1 = xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx.Other(
            ddddddddddddddddddddddddddddddddddd,
            eeeeeeeeeeeeeeeeeeeeee,
            ffffffff
        );
        var a9 = new SomeType(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc).Other(
            ddddddddddddddddddddddddddddddddd,
            eeeeeeeeeeeeeeeeeeeeee,
            ffffffff
        );
        var c6 = (SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)).Other(
            ddddddddddddddddddddddddddddddddddd,
            eeeeeeeeeeeeeeeeeeeeee,
            ffffffff
        );
        var d4 = xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx?.Other(
            ddddddddddddddddddddddddddddddddddd,
            eeeeeeeeeeeeeeeeeeeeee,
            ffffffff
        );
    }

    void DottedHeadsUnchanged() {
        var p1 = alpha.SomeMethod(aaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var b5 = this.SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var c10 = base.SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(ddddddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
        var d10 = SomeType.SomeMethod(aaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc)
            .Other(dddddddddddddddddddddddddd, eeeeeeeeeeeeeeeeeeeeee, ffffffff);
    }

    void FitsAndKept() {
        var s1 = SomeMethod(a, b).Other(c, d);
        var s2 = x.Other(c, d);
        var c13 = SomeMethod(a, b).Other(c, d).Third(e);
        var c12 = SomeMethod(aaa, bbb)
            .Other(ccc, ddd);
    }
}
