class SwitchExpressionArms {
    int Compact(int v) => v switch { 1 => 10, 2 => 20, _ => 0 };

    int Spread(int v) => v switch {
        1 => 10,
        2 => 20,
        _ => 0
    };
}
