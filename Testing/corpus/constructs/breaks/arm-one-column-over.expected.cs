// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-10
// #559, SK-DIV-0449: an arm one column past the margin breaks after its arrow once its head is wide enough for
// the body's kind — 68 before a call on a name, 26 before a member chain, 24 before an operator — and breaks inside
// the body otherwise.

class C {
    object A1(object s) =>
        s switch {
            "kkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A2(object s) =>
        s switch {
            "kkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A3(object s) =>
        s switch {
            "kkkkkkkkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A4(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A5(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A6(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A7(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A8(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A9(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A10(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" => Method(
                aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa,
                bbbb
            ),
            _ => null
        };

    object A11(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                Method(aaaaaaaaaaaaaaaaaaaaaaaaa, bbbb),
            _ => null
        };

    object A12(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                Method(aaaaaaaaaaaaaaaaaaa, bbbb),
            _ => null
        };

    object A13(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                Method(aaaaaaaaaaaaa, bbbb),
            _ => null
        };

    object A14(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                Method(aaaaaaa, bbbb),
            _ => null
        };

    object A15(object s) =>
        s switch {
            "kkk" => aaaa.Value(b)
                .ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A16(object s) =>
        s switch {
            "kkkkkkkkk" => aaaa.Value(b)
                .ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A17(object s) =>
        s switch {
            "kkkkkkkkkkkkkkk" => aaaa.Value(b)
                .ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A18(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A19(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A20(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccccccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A21(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A22(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A23(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A24(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccccccccccccccc(),
            _ => null
        };

    object A25(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccccccccc(),
            _ => null
        };

    object A26(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccccccccc(),
            _ => null
        };

    object A27(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccccccccc(),
            _ => null
        };

    object A28(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa.Value(b).ccccc(),
            _ => null
        };

    object A29(object s) =>
        s switch {
            "kkk" => aaaa
                + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A30(object s) =>
        s switch {
            "kkkkkkkkk" => aaaa
                + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A31(object s) =>
        s switch {
            "kkkkkkkkkkkkkkk" => aaaa
                + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A32(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A33(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A34(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A35(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A36(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A37(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A38(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A39(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A40(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A41(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbbbbbbbb,
            _ => null
        };

    object A42(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbbbbbbbb,
            _ => null
        };

    object A43(object s) =>
        s switch {
            "kkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkkk" =>
                aaaa + bbbbbbbb,
            _ => null
        };
}
