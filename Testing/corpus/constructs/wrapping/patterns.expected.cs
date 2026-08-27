// skala-oracle: resharper=2025.2.6 config=sha256:bd9791d3a6e6a087 profile=SkalaFormatOnly generated=2026-08-27
class Patterns {
    bool ListPattern(int[] xs) => xs is [1, 2, 3];

    bool ListPatternBrokenInSource(int[] xs) {
        return xs is [
            1,
            2
        ];
    }

    bool LongListPattern(int[] xs) {
        return xs is [1111111111, 2222222222, 3333333333, 4444444444, 5555555555, 6666666666, 7777777777, 888888888];
    }

    bool PropertyPattern(object o) => o is Thing { Alpha: 1, Beta: 2 };

    bool PropertyPatternBrokenInSource(object o) {
        return o is Thing { Alpha: 1, Beta: 2 };
    }

    bool LongPropertyPattern(object o) {
        return o is Thing { Alpha: "aaaaaaaaaaaaaaaaaaaaaaaa", Beta: "bbbbbbbbbbbbbbbbbbbbbbbb", Gamma: "cccccccccc" };
    }
}

class Thing {
    public object Alpha { get; set; }
    public object Beta { get; set; }
    public object Gamma { get; set; }
}
