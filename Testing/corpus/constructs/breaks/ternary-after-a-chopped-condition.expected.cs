// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
namespace Constructs.Breaks;

// Issue #530 (SK-DIV-0333). A conditional whose condition spans lines as a chain or an argument list
// puts its `?` and `:` one level past the statement — the dots' level — not one past the condition's
// last line; a broken binary condition is the opposite, its signs a level past the operators.
public class TernaryAfterAChoppedCondition {
    async Task<object> M() {
        var t = someParticularThingWithALongName.SelfLink()
            .SelfLink()
            .SelectName(n => n.Name)
            .WhereSomething(x => x.IsEnabledAndReady)
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
        x = someParticularThingWithALongName.SelfLink()
            .SelfLink()
            .SelectName(n => n.Name)
            .WhereSomething(x => x.IsEnabledAndReady)
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
        Use(
            someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
                ? otherFallbackValueName.SomeFallbackProperty
                : third
        );
        var u = flag
            ? someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
            : third;
        var b1 = someParticularThingWithALongNameeeeeeeeeeeeeeee
            && otherParticularThingWithALongNameeeeeeeeeeeeeeeeeeee
            && thirdddddd
                ? otherFallbackValueName.SomeFallbackProperty
                : third;
        var b2 = someParticularThingWithALongName.SelfLink(
                alphaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            )
            .Selfffff
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
        var a1 = !Compute(
            alphaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            gammaaaaaaaaaaaaaaaaaaaaaaaa
        )
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
        var a2 = (someParticularThingWithALongNameeeeeeeeeeeeeeee
            && otherParticularThingWithALongNameeeeeeeeeeeeeeeeeeee)
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
        var a4 = await ComputeAsync(
            alphaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            gammaaaaaaaaaaaaaa
        )
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
        var a5 = someParticularThingWithALongNameeeeeeeeeeeeeeee
            == otherParticularThingWithALongNameeeeeeeeeeeeeeeeeeeeeeeeee
                ? otherFallbackValueName.SomeFallbackProperty
                : third;
        var a8 = new Foo(
            alphaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            gammaaaaaaaaaaaaaaaaaaaaaaaaaa
        ).Ok
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
        return Compute(
            alphaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            betaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
            gammaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
        )
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
    }

    object P() =>
        someParticularThingWithALongName.SelfLink()
            .SelfLink()
            .SelectName(n => n.Name)
            .WhereSomething(x => x.IsEnabledAndReady)
            ? otherFallbackValueName.SomeFallbackProperty
            : third;
}
