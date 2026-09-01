// ⚠ `readonly` on an accessor is CS8664 unless the property has both a get and a set, and a set in a
// readonly struct may not assign a field — so this narrow shape is the only one the accessor branch can
// ever see. It is legal, measured, and reported.
readonly struct Metre {
    readonly int value;

    public Metre(int value) => this.value = value;

    public int Value {
        readonly get => value;
        set { }
    }
}
