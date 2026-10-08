// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-10-08
// A sole lambda argument whose body is a chain of calls (#571): the arrow breaks for a lambda without
// parentheses from column 21 and one with them from column 25, however long the chain; before that, by
// the measured line a property fill's lambda uses (#557), and when the chain's head no longer fits beside
// an arrow ending at column 21 or later. Otherwise the arrow stays and the chain breaks.

class C {
    void M() {
        UUUUUUUU((x) =>
            source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.Select(y => y).Where(z => z.Bb)
        );
        UUUUUUUUUUUU((x) =>
            source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.Select(y => y).Where(z => z.Bb)
        );
        UU((x) => source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.Select(y => y)
            .Where(z => z.Bb)
        );
        UUUUUUUUUUUU((x) => source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.Select(y => y)
            .Where(z => z.Bb)
        );
        UUUUUUUUUU(x =>
            source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.Select(y => y).Where(z => z.Bb)
        );
        UUUUUUUUUUUU(x =>
            source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.Select(y => y).Where(z => z.Bb)
        );
        UU(x => source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.Select(y => y)
            .Where(z => z.Bb)
        );
        UUUUUUUUUUUUUUUU(x =>
            source.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA.Select(y => y).Where(z => z.Bb)
        );
    }
}
