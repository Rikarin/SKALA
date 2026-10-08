namespace Constructs.Breaks;

// Issue #553. An `=` whose value is a conditional breaks exactly when the condition does not fit beside
// it and the head through the `=` is twelve columns or more — whether the condition then fits below or
// has to chop there; behind `var v =` the `=` stays and the condition breaks. A call condition that fits
// nowhere keeps the `=` and chops its arguments once the `=` stands right of column 40.
public class ConditionalAfterEq {
    object M() {
        var va = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name).WhereSomething(x => x.IsEnabledAndReady) ? otherFallbackValueName.SomeFallbackProperty : third;
        var vaaaaaaaaaa = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name).WhereSomething(x => x.IsEnabledAndReady) ? otherFallbackValueName.SomeFallbackProperty : third;
        var vaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name).WhereSomething(x => x.IsEnabledAndReady) ? otherFallbackValueName.SomeFallbackProperty : third;
        var wa = someParticularThingWithALongNameeeeeeeeeeeeeeee && otherParticularThingWithALongNameeeeeeeeeeeeeeeeeeee && thirdddddddddddddddd ? otherFallbackValueName.SomeFallbackProperty : third;
        var waaaaaaaaaaaaaaaaaaaa = someParticularThingWithALongNameeeeeeeeeeeeeeee && otherParticularThingWithALongNameeeeeeeeeeeeeeeeeeee && thirdddddddddddddddd ? otherFallbackValueName.SomeFallbackProperty : third;
        var xa = Compute(alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, gammaaaaaaaaaaaa) ? otherFallbackValueName.SomeFallbackProperty : third;
        var xaaaaaaaaaaaaaaaaaaaa = Compute(alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, gammaaaaaaaaaaaa) ? otherFallbackValueName.SomeFallbackProperty : third;
        var xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = Compute(alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa, gammaaaaaaaaaaaa) ? otherFallbackValueName.SomeFallbackProperty : third;
        var a7aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name) ? otherFallbackValueName.SomeFallbackProperty : third;
        var a7 = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name) ? otherFallbackValueName.SomeFallbackProperty : third;
        var a8aaaaaaaaaaaaaaaaaaaa = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name) ? otherFallbackValueName.SomeFallbackProperty : third;
        var b1aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = Compute(alphaaaaaaaaaaaaaaaaaaaa, betaaaaaaaaaaaaaaaaaaaaa) ? otherFallbackValueName.SomeFallbackProperty : third;
        var b3aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = someFlagValueeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee ? otherFallbackValueName.SomeFallbackProperty : third;
        x7aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa = someParticularThingWithALongName.SelfLink().SelfLink().SelectName(n => n.Name) ? otherFallbackValueName.SomeFallbackProperty : third;
        var a6 = someParticularThingWithALongNameeeeeeeeeeeeeeee is SomeVeryLongTypeNameeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee ? otherFallbackValueName.SomeFallbackProperty : third;
        return null;
    }
}
