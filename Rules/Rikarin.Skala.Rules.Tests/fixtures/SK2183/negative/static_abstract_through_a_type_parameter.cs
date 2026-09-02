interface IZero<TSelf> where TSelf : IZero<TSelf> {
    static abstract int Zero { get; }
}

sealed class Counter : IZero<Counter> {
    public static int Zero => 0;
}

static class Read {
    // A type parameter qualifier whose member is declared on the interface: generic math, not this.
    public static int Value<T>() where T : IZero<T> => T.Zero;
}
