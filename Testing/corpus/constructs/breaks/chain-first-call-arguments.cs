namespace Constructs.Breaks;

// SK-DIV-0184 (issue #418). An argument list opened on the first line of a chained call that breaks
// after it nests from the chain's continuation line, and its `)` sits on that line with the dots:
// the arguments two levels past the statement, not one. The same holds for the first operand of a
// binary operator that breaks after it (SK-DIV-0149's rows), whatever heads the chain — a name, a
// member access, `this`, a `new`, a dot-less call, a `?.`, a cast, an `await`. Where the construct
// spends no level of its own — a binary inside an argument list, a parenthesised head inside one —
// there is nothing to lift and the list nests the ordinary way.
public class ChainFirstCallArguments {
    async void Roots() {
        var a = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third).Where(beta);
        var b = this.source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
        var c = this.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third, fourth).Where(beta);
        var d = new Foo().Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third).Where(beta);
        var e = Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third, fourth, fifth).Where(beta);
        var f = source?.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third)?.Where(beta);
        var g = (object)source.Select<int>(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
        var h = await source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third).Where(beta);
        var j = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third)[0].Where(beta);
        var k = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third).Count.ToString();
        var l = (left ?? right).Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta);
    }

    object Body() => source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third).Where(beta).ToList();

    object Returned() {
        return source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third).Where(beta).ToList();
    }

    void Owners() {
        source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third, fourth).Where(beta).ToList();
        this.field = source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third).Where(beta);
        if (condition) {
            Outer(first: 1, source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta));
            foreach (var item in source.Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta)) {
                Use(item);
            }
        }

        Outer(first: 1, (left ?? right).Select(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other).Where(beta));
    }

    void Binaries() {
        var x = source.F(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third, fourth) ?? fallback;
        var y = F(
            first,
            second
        )
            + 1;
        var ok = items.Any(x => {
            First();
            return x;
        }
        )
            && flag;
        Outer(first: 1, source.F(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third) ?? fallback);
    }

    void Nested() {
        var x = source.Select(Inner(selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe, other, third)).Where(beta);
        var y = source.Select(x => {
            First();
            Second(x);
        }).Where(beta);
        var z = source.Select(x => {
            First();
            Second(x);
        }, selectorWithAVeryLongNameThatWillNotFitBesideTheChainHeadAtAnyIndentWeMeasuredInTheProbe).Where(beta);
    }
}
