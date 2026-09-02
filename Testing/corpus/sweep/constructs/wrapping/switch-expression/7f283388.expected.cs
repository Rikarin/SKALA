// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
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
