// `!=` cannot stand here unparenthesised, and emitting the parentheses would hand `SK0209` a
// finding on this rule's own output.
class C {
    public static bool Run(int left, int right, bool other) => !(left == right) == other;
}
