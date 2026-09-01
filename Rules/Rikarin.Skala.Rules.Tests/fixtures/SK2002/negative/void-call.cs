using System.Diagnostics.Contracts;

class C {
    [Pure]
    static void Validate() { }

    void M() {
        Validate();
    }
}
