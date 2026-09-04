// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class Patterns {
    bool Property(object o) => o is {
        First: 1,
        Second: 2
    };

    bool PropertyOnOneLine(object o) => o is { First: 1, Second: 2 };

    bool List(int[] o) => o is [
        1,
        2
    ];

    bool ListOnOneLine(int[] o) => o is [1, 2];
}
