// skala-oracle: resharper=2025.2.6 config=sha256:14c031ee7ef4b616 profile=SkalaFormatOnly generated=2026-09-02
class SwitchExpressions {
    int OnOneLine(int v) => v switch { 1 => 10, _ => 0 };

    int Broken(int v) => v switch {
        1 => 10,
        _ => 0
    };
}
