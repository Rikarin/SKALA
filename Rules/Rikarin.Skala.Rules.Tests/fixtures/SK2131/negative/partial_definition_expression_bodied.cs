// ⚠ #397. A C# 13 partial property's defining declaration is spelled `{ get; }`, which is exactly an
// auto-property, and the implementation that computes the value is a second declaration this shape
// never reads. `Count` is 42, not `default`. A definition with no implementation is CS9248, so a
// definition is never the declaration that is left holding `default`.
sealed partial class Loader {
    public partial int Count { get; }

    public partial int Count => 42;
}
