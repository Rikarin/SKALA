// ⚠ docs/plan/07 § "Metrics": "including primary-constructor parameters". A primary constructor is
// the type's constructor whatever the syntax, and twelve of them is twelve positional arguments at
// every call site.
public sealed class WidePrimaryConstructor(int a0, int a1, int a2, int a3, int a4, int a5, int a6, int a7, int a8, int a9, int a10, int a11) {
    public int First => a0;
}
