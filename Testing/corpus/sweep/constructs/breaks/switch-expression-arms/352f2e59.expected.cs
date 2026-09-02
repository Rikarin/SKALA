// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
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
