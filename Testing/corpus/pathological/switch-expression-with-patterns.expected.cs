// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    int M(object o) =>
        o switch {
            int i and > 0 => i,
            string { Length: > 2 } s => s.Length,
            [1, .., 3] => 0,
            _ => -1
        };
}
