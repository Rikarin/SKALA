class C {
    int M(object o) =>
        o switch {
            int i and > 0 => i,
            string { Length: > 2 } s => s.Length,
            [1, .., 3] => 0,
            _ => -1
        };
}
