// skala-oracle: resharper=2025.2.6 config=sha256:1db666f69fec005d profile=SkalaFormatOnly generated=2026-08-30
class SwitchExpressionArms {
    int Compact(int v)
        => v switch {
            1 => 10,
            2 => 20,
            _ => 0
        };

    int Spread(int v)
        => v switch {
            1 => 10,
            2 => 20,
            _ => 0
        };
}
