using System;

sealed class PrimaryConstructorFixture(int factor) {
    public Func<int, int> Scale() => value => value * factor;
}
