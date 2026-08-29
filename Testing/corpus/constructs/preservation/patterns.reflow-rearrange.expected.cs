// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-29
class Patterns {
    bool Property(object o) => o is { First: 1, Second: 2 };

    bool PropertyOnOneLine(object o) => o is { First: 1, Second: 2 };

    bool List(int[] o) => o is [1, 2];

    bool ListOnOneLine(int[] o) => o is [1, 2];
}
