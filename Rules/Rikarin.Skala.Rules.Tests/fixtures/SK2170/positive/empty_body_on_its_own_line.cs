// ⚠ `CS0642` is silent here, and that is the whole reason this is a positive. It reports an empty
// loop body only when a *block* follows the `;`; here an ordinary statement follows, aligned with
// the `;`, and the compiler says nothing while the layout says `Use(0)` is inside the loop.
class C {
    void M(bool flag) {
        while (Step(flag))
            ;
            Use(0);
    }

    static bool Step(bool flag) => flag;

    static void Use(int value) { }
}
