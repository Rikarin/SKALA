// The other side of `negative/a_nullable_tuple_target_does_not_parse.cs`, and the reason that one is
// a syntax question rather than a "nullable types are unsafe" question. `typeof(int?)` emits
// `value is int?`, which parses as a type test and compiles — `int?` has no leading `(` to send the
// parser down the pattern path, so it never becomes a conditional expression the way
// `(int, int)?` does.
//
// ⚠ Declining every nullable target would turn this red, and dropping the parse check would turn
// the other red. Neither fixture alone says where the boundary is.
public static class NullableValueTypeTarget {
    public static bool IsCount(object value) => typeof(int?).IsInstanceOfType(value);
}
