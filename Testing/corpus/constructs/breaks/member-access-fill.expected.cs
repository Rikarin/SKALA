// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
namespace Constructs.Breaks;

// Issue #482 (SK-DIV-0124). A member-access expression that is no chain of calls breaks once, at the
// last of its dots that still fits, in preference to an `=`, a `return`'s line or a switch arm's
// arrow with a short body; an argument list chops first and the dots break inside the argument.
// Not as an assignment's target, not as the operand of `is` or `as`, and not before a switch arm's
// arrow with a body of fourteen columns or more, where the other break is taken.
public class MemberAccessFill {
    int N(object o) =>
        o switch {
            Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
                .MorexxxxxxxxxxxxxxxxxValue => 2u,
            Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
                .MorexxxxxxxxxxxxxxValue => yyy,
            Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
                .MorexxxxxxxxxxxValue => yyyyyyyyyyyyy,
            Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.MorexxxxxxxxxxxValue =>
                yyyyyyyyyyyyyy,
            Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.MorexxxxValue =>
                SomeIdentifierOfMedium,
            Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.MorexxxxxxxxxxxxxxxxxxxxValue
                => SomeIdentifierOfMedium,
            _ => 0
        };

    object P() {
        var value = Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
            .MorexxxxxxxxxxxxxxxxxValueeee;
        var value2 = Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
            .MorexxxxxxxxxxxxxxxxxValueeee.Rest;
        var value3 = Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
            .Eeeeeeeeeeeeeeeeeeeeeeeee.Ffffffffffffffffff.Gggggggggggggggggggggg.Hhhhhhhhhhhhhhhhhhhhhhhhhhhhhhhh
            .Iiiiiiiiiiiiiiiii;
        var value4 = alphaaaaaaaaaaaaaaaa.Betaaaaaaaaaaaaaaaaaa[0].Gammaaaaaaaaaaaaaa.Deltaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            .Epsilon;
        var value5 = alphaaaaaaaaaaaaaaaa?.Betaaaaaaaaaaaaaaaaaa.Gammaaaaaaaaaaaaaa.Deltaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            .Epsilon;
        var value6 = alphaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa[0]
            .Propertyyyyyyyy;
        var value7 = alphaaaaaaaaaaaaaaaa.Betaaaaaaaaaaaaaaaaaa.Gammaaaaaaaaaaaaaa.Method(
            aaaaaaaaaaaaaa,
            bbbbbbbbbbbbbbbbbbbbbbbbbbb
        );
        var aaaaaaaaaa = Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
            .Morexxxxxyyyyyyyyyyyy;
        Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.MorexxxxxxxxxxxxxxxValue =
            yyyyyyyy;
        Use(
            Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd
                .MorexxxxxxxxxxxxxxxxxValueeeeeeeee
        );
        Foo(
            Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.Eeeeeeeeeeeeeeeeeeeee,
            second
        );
        if (Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.Eeeeeeeeeeeeeeeeeeeeeeeeeee
            == x) { }

        bool b2 =
            someVeryLongReceiverName.SomeVeryLongPropertyName.AnotherVeryLongPropertyNameXYZ as SomeVeryLongTypeName;
        return Aaaaaaaaaaaaaaaa.Bbbbbbbbbbbbbbbbbbbb.Cccccccccccccccccccc.Dddddddddddddddddd.Eeeeeeeeeeeeeeeeeeeeeeeee
            .Ffffffff;
    }
}
