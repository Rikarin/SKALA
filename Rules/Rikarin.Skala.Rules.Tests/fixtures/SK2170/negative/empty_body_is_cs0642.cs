// ⚠ `CS0642`, "possible mistaken empty statement", covers every one of these and is on by default:
// `if`, `else`, `lock`, `do`, `using` and `fixed` outright, and `while`, `for` and `foreach` exactly
// when a block follows the `;` — which is the whole of the shape that misleads. Measured on SDK
// 10.0.400, not assumed. SK2170 adds nothing here and stays silent.
class C {
    void M(bool flag, int[] data) {
        while (Step(flag)) ;
        {
            Use(0);
        }

        for (var i = 0; i < data.Length; i++) ;
        {
            Use(1);
        }

        if (flag) ;
        {
            Use(2);
        }
    }

    static bool Step(bool flag) => flag;

    static void Use(int value) { }
}
