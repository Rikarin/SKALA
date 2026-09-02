using System;

public sealed class Lambdas {
    public Func<int> Make() {
        var f = (Func<int>)(() => 1);
        return f;
    }
}
