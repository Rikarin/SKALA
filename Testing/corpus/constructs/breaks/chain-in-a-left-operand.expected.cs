// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
namespace Constructs.Breaks;

// Issue #457 (SK-DIV-0068 item 3). A chopped chain that is the left operand of a binary operator that
// broke nests from the operator's continuation line: its dots two levels past the statement, the
// operator one — for `+`, `??`, `&&`, a run of operators, after `var x =`, an assignment, `return`, an
// expression body, inside an argument and in an aligned `if` condition. As the right operand the chain
// opens on the operator's own line and nests from it the ordinary way.
public class ChainInALeftOperand {
    bool M() {
        var w = someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
            + otherFallbackValueName.SomeFallbackProperty;
        var v = someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
            ?? otherFallbackValueName.SomeFallbackProperty;
        var r = otherFallbackValueName.SomeFallbackProperty
            + someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady);
        var u = someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
            + otherFallbackValueName.SomeFallbackProperty
            + third;
        Use(
            someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
            + otherFallbackValueName.SomeFallbackProperty
        );
        Use(
            first,
            someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabled)
            + otherFallbackValueName.SomeFallbackProperty
        );
        if (someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
            && otherFallbackValueName.SomeFallbackProperty) {
            A();
        }

        x = someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
            + otherFallbackValueName.SomeFallbackProperty;
        var k = someParticularThingWithALongName.SelfLink().SelectName(n => n.Name)
            + otherFallbackValueName.SomeFallbackPropertyyyyyyyyyy;
        return someParticularThingWithALongName.SelfLink()
                .SelfLink()
                .SelectName(n => n.Name)
                .WhereSomething(x => x.IsEnabledAndReady)
            && otherFallbackValueName.SomeFallbackProperty;
    }

    object P() =>
        someParticularThingWithALongName.SelfLink()
            .SelfLink()
            .SelectName(n => n.Name)
            .WhereSomething(x => x.IsEnabledAndReady)
        + otherFallbackValueName.SomeFallbackProperty;
}
