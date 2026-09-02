// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaFormatOnly generated=2026-09-02
class C {
    int[] M() => new[] { 1, 2 };

    // ⚠ Five elements is over `max_initializer_elements_on_line = 4`, so this one is chopped
    // whatever its width — which is what makes the indent inside the braces observable at all.
    System.Collections.Generic.List<int> N() =>
        new System.Collections.Generic.List<int> {
            1,
            2,
            3,
            4,
            5
        };
}
