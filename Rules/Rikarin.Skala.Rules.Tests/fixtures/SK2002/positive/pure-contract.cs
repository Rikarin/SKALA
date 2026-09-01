using System.Diagnostics.Contracts;

class C {
    [Pure]
    static int Calculate(int x) => x + 1;

    void M() {
        Calculate(1);
    }
}
