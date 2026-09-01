// ⚠ Resolved by namespace-qualified name. An attribute of the author's own that happens to be
// spelled `ThreadStatic` is not `System.ThreadStaticAttribute` and is never touched.
namespace Acme {
    [System.AttributeUsage(System.AttributeTargets.Field)]
    sealed class ThreadStaticAttribute : System.Attribute { }

    sealed class Cache {
        [ThreadStatic] int entries = 4;

        public int Entries => entries;
    }
}
