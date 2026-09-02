// A user-defined indexer outside the positional contracts is free to give a negative argument any
// meaning at all — here it counts backwards, which is exactly the meaning the rule must not assume
// is absent.
class Wrapped {
    readonly int[] values = new int[4];

    public int this[int index] => index < 0 ? this.values[this.values.Length + index] : this.values[index];
}

class C {
    int Before(Wrapped wrapped) => wrapped[-1];
}
