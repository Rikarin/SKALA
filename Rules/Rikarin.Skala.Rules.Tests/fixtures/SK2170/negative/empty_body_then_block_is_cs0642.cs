// ⚠ The narrow overlap with `CS0642`, and the fixture that makes the guard reachable. An empty body
// followed by a *block* is exactly what the compiler reports — for `if`, `else`, `lock`, `do`,
// `using` and `fixed` outright, and for `while`, `for` and `foreach` when a block follows, measured
// on SDK 10.0.400. Here the block is aligned with the `;`, so every other condition of this rule
// holds and only that guard declines it.
class C {
    void M(bool flag) {
        while (Step(flag))
            ;
            {
                Use(0);
            }
    }

    static bool Step(bool flag) => flag;

    static void Use(int value) { }
}
