// skala-oracle: resharper=2025.2.6 config=sha256:381a31a28c5ea94d profile=SkalaFormatOnly generated=2026-08-28
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
