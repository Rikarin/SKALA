// No suppression, so nothing is being asserted away.
namespace Fixtures {
    interface IClock { }

    sealed class Host {
        public object? Resolve(System.IServiceProvider provider) => provider.GetService(typeof(IClock));
    }
}
