namespace Constructs.Breaks;

// SK-DIV-0206 (issue #445). A break around `is` or `as` lands one level past the line its operand starts
// on, and not one level past everything open there: inside a lambda that is an argument, four columns
// past the call's line, not eight. A list opened on the operand's line keeps its own one level, and an
// author's break before the keyword lifts it to the keyword's line.
public class TypeTestLevel {
    void M() {
        var a10 = collection.Elements.All(static element => element
            is ExpressionElementSyntax);
        var a1 = nodes.Count(static collection => collection.Parent
            is Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax);
        var a2 = nodes.Count(static collection => collection.Parent.SomeVeryLongPropertyNameXYZWVUTSRQPONMLKJIHGFEDCBA is SomeVeryLongTypeName);
        var a3 = Compute(collection.Parent
            is ArgumentSyntax);
        var a4 = Compute(alpha, collection.Parent.SomeVeryLongPropertyNameXYZWVUTSRQPONMLKJIHGFEDCBA_AndMoreAndMore is SomeType);
        var a5 = nodes
            .Where(static collection => collection.Parent
                is ArgumentSyntax)
            .Count();
        Use(x => x
            is string);
        if (Compute(alpha,
                beta) is string) { }
        var a6 = Compute(
            alpha,
            beta) is string;
        var a7 = Compute(
                alpha,
                beta)
            is string;
        bool a8 = flag
            && Compute(alphaArgumentValue, betaArgumentValue, gammaArgumentValue, deltaArgumentValueXYZ) is SomeLongType;
    }

    bool P(object o) => o
        is string;

    object Q(object o) {
        return o
            is string;
    }
}
