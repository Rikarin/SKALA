// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A local's `=` before `operand is A or B` past the margin (#446, SK-DIV-0211): with a head of 12 or more
// the oracle breaks the `=` unless the pattern is wider than a measured threshold that falls with the
// line's end; with a narrower head only for a narrow pattern. After the `=` breaks, the pattern moves below
// the `is` once the operand and its first operand no longer fit — sooner for a first operand of three
// columns or fewer. Each pair sits either side of a measured threshold.

class EqualsBeforeABinaryPattern {
    void M() {
        bool c =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaa or Bbbbbbbbbbbb;
        bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaa
            or Bbbbbbbbbbbb;
        bool c =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaa or Bbbbbbbbbbbb;
        bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaa
            or Bbbbbbbbbbbbb;
        bool c =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaa
                or Bbbbbbbbbbbbb;
        bool c = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaa
            or Bbbbbbbbbbbbb;
        bool ccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaaaa or Bbbbbbbbbbbbbbbb;
        bool ccc = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaaaaa
            or Bbbbbbbbbbbbbbbb;
        bool ccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaaaaa or Bbbbbbbbbbbbbbbb;
        bool ccc = ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaaaaa
            or Bbbbbbbbbbbbbbbbb;
        bool ccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaaaaa
                or Bbbbbbbbbbbbbbbbb;
        bool ccc = oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaaaaaaaaaaaaaaa
            or Bbbbbbbbbbbbbbbbb;
        bool ccccc =
            ooooooooooooo is Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa or Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
        bool ccccc = oooooooooooo is Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            or Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
        bool ccccc =
            oooooooooooooooooooooooooo is Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                or Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
        bool ccccc = ooooooooooooooooooooooooo is Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            or Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
        bool ccccccccccccc =
            ooooooooooooooooooooo is Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
                or Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
        bool ccccccccccccc = oooooooooooooooooooo is Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            or Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
        bool ccccccccccccccccccccccc =
            ooooooooooooo is Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa or Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
        bool ccccccccccccccccccccccc = oooooooooooo is Aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa
            or Bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb;
        bool ccccccccccccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is > 5 and < 10;
        bool ccccccccccccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is > 5
                and < 10;
        bool ccccccccccccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is null or Empty;
        bool ccccccccccccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is null
                or Empty;
        bool ccccccccccccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Alpha or Beta or Gamma;
        bool ccccccccccccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Alpha
                or Beta
                or Gamma;
        bool ccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                A or Bbb;
        bool ccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                A or Bbb;
        bool ccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                A or Bbb;
        bool ccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaa
                or Bbb;
        bool ccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                Aaa or Bbb;
        bool ccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is
                Aaa or Bbb;
        bool ccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaa
                or Bbb;
        bool ccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaa
                or Bbb;
        bool ccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is Aaaa
                or Bbb;
        bool ccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is > 5
                and < 10;
        bool ccc =
            oooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is > 5
                and < 10;
        bool ccc =
            ooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooooo is > 5
                and < 10;
    }
}
