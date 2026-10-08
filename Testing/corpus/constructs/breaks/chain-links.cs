namespace Constructs.Breaks;

// Issues #454, #455, #456 (SK-DIV-0066, SK-DIV-0067, SK-DIV-0068, SK-DIV-0184): what a chained call's
// links are. A chain that ends in a property run is a chain, and the run is its last link, broken
// before its first dot. A property run feeding a call reaches left across a `?`. A `!` ends the
// receiver, so the call after it is the chain's first, and the operand of the `!` breaks as a chain of
// its own. An indexer — `?[0]` included — is a call at the head, and an indexed property after a call
// is a link of its own. A trailing property after an indexer head alone is no chain.
public class ChainLinks {
    void Trailing() {
        var result = someCollectionOfThingsHere.Where(c => c.IsEnabled).Select(c => c.Name).OrderBy(n => n).ToList().Count;
        var y = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaa).Where(beta).Count;
        var y2 = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaa).Where(beta).Count.Value;
        var y3 = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaa).Where(beta).Count?.Value;
        var y4 = source.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaa).Count.Where(beta);
        var y5 = source.Count.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gamxaaaaaaaaaaaaaaaaaaaaaaaa).Countxxxxx;
        var y7 = sourceWithAVeryLongNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee[0].Select(alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa).Count;
        var a = SomeMethod(aaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccc).Property;
        var b = alpha.SomeMethod(aaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccc).Property;
        var c = alpha[0].SomeMethod(aaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, cccccccccccccccc).Property;
        var d = SomeMethod(aaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccccc)?.Property;
        var e = alpha.SomeMethod(aaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccccc).Prop.Erty;
        var h = alpha.SomeMethod(aaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbb).Other(cccccccccccccccccccccccccc).Property;
        var v2 = Method(aaaaaaaaaaaaaaaaaaaaaaaaaaaa, bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb, ccccccccccccccc).Prop.Ertyyyy;
    }

    void Bang() {
        var bang = receiverWithAVeryLongNameIndeed.SelfLink()!.SelfLink().SelfLink().SelectName(n => n.Name).ToList().Count();
        var x3 = source!.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValuexx).Where(beta).ToList();
        var x4 = sourceeeeeeeeeeeeeeeeeeeee.Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo).Where(gammaArgumentValuexxxxx)!.Where(beta).ToList();
    }

    void Indexers() {
        var elemc = sourceWithAVeryLongName?[0].Children.Where(item => item.IsEnabled).Select(item => item.Name).ToList();
        var elemd = sourceWithAVeryLongName?[0].Where(item => item.IsEnabled).Select(item => item.Name).ToList().Countxxxxxxx();
        var x5 = source.Items[0].Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValuexxxxxxxxx);
        var x6 = source.Make().Items[0].Select(alphaArgumentValueNumberOne, betaArgumentValueNumberTwo, gammaArgumentValuexxxxxxxxx);
        var x7 = source.Make().Items[0].Selectttttttttttttt(alphaArgumentValueNumberOne).Where(betaArgumentValueNumberTwooooooooo);
    }

    void Straddle() {
        var c = someParticularThingWithALongName.Self().Inner?.Children.Where(item => item.IsEnabled).Select(item => item.Name).ToList();
        var c2 = someParticularThingWithALongName.Self().Outer.Inner?.Children.Where(item => item.IsEnabled).Select(item => item.Name);
        var c3 = someParticularThingWithALongName.Self().Inner?.Children?.Where(item => item.IsEnabled).Select(item => item.Name).ToList();
        var c4 = someParticularThingWithALongName.Self()?.Inner?.Children.Where(item => item.IsEnabled).Select(item => item.Name).ToList();
    }

    object E() =>
        (
            a)[0].C;

    object F() =>
        (
            a).B().C;

    object G() =>
        (
            a).B().C().D;

    object H() =>
        (
            a)[0].C().D;

    object a;
}
