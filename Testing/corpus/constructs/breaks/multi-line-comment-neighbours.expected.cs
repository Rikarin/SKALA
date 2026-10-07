// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-07
namespace Constructs.Breaks;

// SK-DIV-0205 (issue #440). The neighbours of SK-DIV-0200 around a block comment that spans lines. A
// comment's own line breaks are not an author's break between two tokens, so a ternary holding one
// chops at both signs. `is` and `as` wrap after the keyword, never before it, one level past the line
// and on the condition's column in a header. An array initializer's `{` and a collection expression's
// `[` keep their first element after such a comment, where an object or collection initializer breaks.
// A fill measures an item only up to the comment's first line, and an item ending in one ends its line.

public class MultiLineCommentNeighbours1 {
    void M() {
        var t = c /* a
          b */
            ? 1
            : 2;
        var t2 = c /* a */ ? 1 : 2;
        var t3 = c
            ? 1 /* a
              b */
            : 2;
        var t4 = c ? 1 : 2 /* a
          b */;
        var t5 = c /* a
          b */
            ? x ? 1 : 2
            : 3;
        var t6 = Compute(c) /* a
          b */
            ? 1
            : 2;
        bool b1 = o is /* gap
          gap2 */ string;
        bool b2 = o as /* gap
          gap2 */ string;
        bool b3 = o is /* gap */ string;
        bool b4 = o /* gap
          gap2 */ is string;
        bool b5 = a
            && o is /* gap
              gap2 */ string;
        bool b6 = o is /* gap
          gap2 */ string
            && a;
        bool b7 = o is /* gap
          gap2 */ { Length: 1 };
        var z = new[] { /* a
          b */ 1, 2
        };
        var z2 = new int[] { /* a
          b */ 1, 2
        };
        var z3 = new List<int> { /* a
          b */
            1, 2
        };
        int[] z4 = { /* a
          b */ 1, 2
        };
        var z5 = new Point { /* a
          b */
            X = 1, Y = 2
        };
        int[] z6 = [ /* a
          b */ 1, 2
        ];
        var z7 = new[] { /* a */ 1, 2 };
        var z8 = new[] {
            /* a
          b */ 1, 2
        };
        var z9 = new[] {
            1, 2, /* a
              b */ 3
        };
        var z10 = new Dictionary<int, int> { /* a
          b */
            [1] = 2
        };
        var z11 = new[] { /* a
          b */ 1, 2, 3333333333, 4444444444, 5555555555, 6666666666, 7777777777, 88888888888, 99999999999, 1010101010
        };
    }

    bool B(object o) =>
        o is /* gap
          gap2 */ string;

    bool B2(object o) =>
        o is /* gap
          gap2 */ string
        || o is /* gap
        gap2 */ int;
}

public class MultiLineCommentNeighbours2 {
    void M() {
        bool b1 = someVeryLongReceiverName.SomeVeryLongPropertyName.AnotherVeryLongPropertyName is SomeVeryLongTypeName;
        bool b2 =
            someVeryLongReceiverName.SomeVeryLongPropertyName.AnotherVeryLongPropertyNameXYZ as SomeVeryLongTypeName;
        bool b3 =
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyName is
                SomeVeryLongTypeName;
        var b4 =
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyName as
                SomeVeryLongTypeName;
        bool b5 =
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyName is
                not SomeVeryLongTypeName;
        bool b6 =
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyNam is {
                Length: 1
            };
        bool b7 = o
            is string;
        bool b8 = o
            as string;
        bool b9 = Compute(alphaArgument, betaArgument) /* gap
          gap2 */ is string;
        var b10 = o /* a */ is string;
        bool b11 = aaaa
            && someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_Anoth is
                SomeVeryLongTypeName;
    }
}

public class MultiLineCommentNeighbours3 {
    void M() {
        bool b5 =
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyName is
                not SomeVeryLongTypeName;
        bool c1 =
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyNam is
                null;
        bool c2 = someVeryLongReceiverName.SomeVeryLongPropertyName.AnotherVeryLongPropertyNameXYZWVUTS is not null
            && aaaa;
        bool c4 =
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLong is string text;
        bool c5 = someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLong is SomeVeryLongTypeName
            or AnotherVeryLongTypeName;
        bool c6 = o is /* a
          b */ not null;
        bool c7 = o
            is not null;
        bool c8 = o is
            not null;
        bool c9 = o is
            string;
        if (someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyNam is
            string) { }

        if (someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyNam is
            not null) { }

        var d1 = Compute(
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVer as string
        );
        var d2 = Compute(
            someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA,
            someVeryLongPropertyName_AnotherVery is string
        );
        return;
    }

    bool P(object someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA) =>
        someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA is string;

    bool Q(object someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA) =>
        someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_Some is string;
}

public class MultiLineCommentNeighbours4 {
    void M() {
        if (someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyNam is
            string) { }

        if (aaaa
            && someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPro is
                string) { }

        if (Compute(
                someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPro is string
            )) { }

        while (someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropert is
               string) { }

        if (someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyNam as
                string
            == null) { }
    }

    bool B =>
        someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyNamXYZWVU is
            string;

    bool D =
        someVeryLongReceiverNameXYZWVUTSRQPONMLKJIHGFEDCBA_SomeVeryLongPropertyName_AnotherVeryLongPropertyNamXYZWVU is
            string;
}

public class MultiLineCommentNeighbours5 {
    void M() {
        var z9 = new[] {
            1, 2, /* a
              b */ 3
        };
        var y1 = new[] {
            1, 2 /* a
              b */,
            3
        };
        var y2 = new[] {
            11111111, 22222222, 33333333, 44444444, 55555555, 66666666, 77777777, 88888888, /* a
              b */ 99999999, 10101010, 11111111, 12121212, 13131313, 14141414, 15151515, 16161616, 17171717, 18181818
        };
        var y3 = new[] {
            11111111, 22222222, 33333333, 44444444, 55555555, 66666666, 77777777, 88888888,
            99999999, /* a comment that is long
              b */ 10101010
        };
        var y4 = new[] {
            11111111, 22222222, 33333333, 44444444, 55555555, 66666666, 77777777, 88888888, 99999999, 10101010,
            1111 /* a
              b */,
            2
        };
        var y5 = new[] {
            1, /* a
              b */ 2, /* c
              d */ 3
        };
        var y6 = new[] { 1, 2, 3 } /* a
          b */;
        byte[] y7 = [
            1, 2, /* a
              b */ 3
        ];
    }
}

public class MultiLineCommentNeighbours6 {
    void M() {
        int[] a1 = [
            1, 2 /* a
              b */,
            3
        ];
        int[] a2 = [
            1, 2, /* a
              b */ 3, 4
        ];
        var a3 = new List<int> {
            1,
            2 /* a
              b */,
            3
        };
        var a5 = new[] {
            11111111, 22222222, 33333333, 44444444, 55555555, 66666666, 77777777, 88888888, 99999999, 10101010,
            11111111, /* a
              b */ 12121212
        };
        var a7 = new[] {
            1, /** a
              b */ 2, 3
        };
        if (x is [
                1, 2 /* a
                  b */,
                3
            ]) { }

        var a8 = new[] { "a", "b" } /* a
          b */;
    }
}
