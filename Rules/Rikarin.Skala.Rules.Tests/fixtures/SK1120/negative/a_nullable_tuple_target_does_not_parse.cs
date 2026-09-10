// ⚠ The one emitted spelling that does not parse **anywhere**, and it is not an expression-tree
// problem at all — it was found while measuring one (#349). The type is re-spelled from the
// `typeof` operand's source text, so this would emit `x is (int, int)?`, and the grammar after `is`
// prefers a pattern: the parser takes `(int, int)` as one and then reads `?` as the start of a
// conditional expression. **CS1003, "':' expected"**, compiled and confirmed — a fix that does not
// parse, in ordinary method bodies as much as in a lambda.
//
// ⚠ `typeof(int?)` is fine and is reported, because `int?` has no leading `(` to send the parser
// down the pattern path. The two spellings differ only in whether the nullable type's underlying
// type is written with parentheses, which is why the guard asks the parser what the emitted text
// came out as instead of listing the type syntaxes that misbehave.
public static class NullableTupleTarget {
    public static bool IsPair(object value) => typeof((int, int)?).IsInstanceOfType(value);
}
