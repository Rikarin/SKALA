// Another type's initializers run on first access to that type, not in this type's order — so
// `Source` being written below `Reader` says nothing about when `Value` is assigned.
//
// ⚠ `Source` is below `Reader` on purpose. In the first version of this fixture it was above, and
// the declaration-order test declined the pair before the containing-type test was ever reached —
// so breaking the containing-type test changed nothing and the sabotage stayed green.
static class Reader {
    public static readonly int Copy = Source.Value;
}

static class Source {
    public static readonly int Value = 42;
}
