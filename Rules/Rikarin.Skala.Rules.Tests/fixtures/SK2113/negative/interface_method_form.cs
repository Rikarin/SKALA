// ⚠ Declined for the fix rather than for the finding: the counterpart of the non-generic call
// lives in an extension class this file need not have imported, and a fix that does not compile is
// worse than no fix.
namespace Fixtures {
    interface IClock { }

    sealed class Host {
        public IClock Resolve(System.IServiceProvider provider) => (IClock)provider.GetService(typeof(IClock))!;
    }
}
