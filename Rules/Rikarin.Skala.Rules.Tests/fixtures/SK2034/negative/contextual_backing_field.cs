// ⚠ Load-bearing. `field` is the backing-field keyword inside an accessor (SK1003's territory), so
// `@field` is the only way to reach an ordinary member of that name — a disambiguation the author
// had no choice about, not a name to change.
class C {
    int @field;

    public int Value {
        get => @field;
        set => @field = value;
    }
}
