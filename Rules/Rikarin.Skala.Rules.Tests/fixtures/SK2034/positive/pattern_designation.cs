// The shape this actually takes in the wild: a pattern variable named after the thing it matched.
class C {
    static bool M(object node) => node is string @string && @string.Length > 0;
}
