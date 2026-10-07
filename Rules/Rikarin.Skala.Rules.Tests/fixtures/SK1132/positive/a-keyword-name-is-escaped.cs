// ⚠ `(string @class, int Size)` names its first element `class`. The fix writes `@class`; a bare `class`
// after the dot does not parse.
public static class Styles {
    public static string Of((string @class, int Size) style) => style.Item1;
}
