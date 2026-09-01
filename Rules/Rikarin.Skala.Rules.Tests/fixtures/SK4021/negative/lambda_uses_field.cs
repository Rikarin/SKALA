using System;

sealed class LambdaUsesFieldFixture {
    readonly int factor = 2;

    public Func<int, int> Use() => Scale();

    Func<int, int> Scale() => value => value * factor;
}
