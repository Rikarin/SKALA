// A member a struct inherits from `object` or `ValueType` is called on a boxed copy whatever the
// capture's storage, so `readonly` does not change it (#412) — measured, with the struct below
// mutable. `int`'s own members are `readonly`, being a `readonly struct`'s.
public struct Plain {
    public int Value;

    public void Set(int value) => Value = value;
}

struct Summary(Plain plain, int count) {
    public int Hash => plain.GetHashCode() - plain.GetHashCode() + count.CompareTo(0) + count.ToString().Length;
}

public static class Probe {
    public static int Run() {
        var plain = new Plain();
        plain.Set(3);
        return new Summary(plain, 42).Hash;
    }
}
