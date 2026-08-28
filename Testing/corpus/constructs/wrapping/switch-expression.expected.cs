// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
class SwitchExpressions {
    int Broken(int value) {
        return value switch {
            1 => 1,
            2 => 2,
            _ => 0
        };
    }

    int OneLineInSource(int value) =>
        value switch {
            1 => 1,
            _ => 0
        };

    int TooWide(int value) =>
        value switch {
            1 => 1111111111,
            2 => 2222222222,
            3 => 3333333333,
            _ => 4444444444
        };
}
