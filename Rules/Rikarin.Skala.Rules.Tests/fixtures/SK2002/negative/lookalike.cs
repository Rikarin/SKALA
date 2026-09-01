using System;

class PureAttribute : Attribute { }

class C {
    [Pure]
    int Compute() => 1;

    public void Trim() { }

    void M() {
        Compute();
        Trim();
    }
}
