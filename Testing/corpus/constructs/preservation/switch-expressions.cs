class SwitchExpressions {
    int OnOneLine(int v) => v switch { 1 => 10, _ => 0 };

    int Broken(int v) => v switch {
        1 => 10,
        _ => 0
    };
}
