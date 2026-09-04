// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
class SwitchExpressionArms {
    int Compact(int v) =>
        v switch {
            1 => 10, 2 => 20, _ => 0
        };

    int Spread(int v) =>
        v switch {
            1 => 10, 2 => 20, _ => 0
        };
}
