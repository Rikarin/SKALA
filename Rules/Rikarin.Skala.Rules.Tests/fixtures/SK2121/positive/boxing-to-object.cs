// Boxing an `int` into `object` always succeeds; there is no other answer the runtime can give.
sealed class Consumer {
    public object? Box(int value) => value as object;
}
