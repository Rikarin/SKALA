// ⚠ `(long)count` binds to `count` alone. Hoisting `long` into the declaration would change
// which multiplication happens.
public sealed class Arithmetic {
    public long Total(int count, int size) {
        var total = (long)count * size;
        return total;
    }
}
