// skala-oracle: resharper=2025.2.6 config=sha256:98ff52570e019fac profile=SkalaFormatOnly generated=2026-08-27
class SwitchExpressions {
    int OnOneLine(int v) =>
        v switch {
            1 => 10,
            _ => 0
        };

    int Broken(int v) =>
        v switch {
            1 => 10,
            _ => 0
        };
}
