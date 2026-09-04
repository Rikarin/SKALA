// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class SwitchExpressions {
    int OnOneLine(int v) => v switch { 1 => 10, _ => 0 };

    int Broken(int v) => v switch {
        1 => 10,
        _ => 0
    };
}
