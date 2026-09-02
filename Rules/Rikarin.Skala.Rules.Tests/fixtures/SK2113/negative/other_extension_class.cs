// A user-defined `GetService<T>` is not this method; the rule resolves by containing type.
namespace Fixtures {
    interface IClock { }

    static class MyLocator {
        public static T? GetService<T>(this System.IServiceProvider provider) => default;
    }

    sealed class Host {
        public IClock Resolve(System.IServiceProvider provider) => provider.GetService<IClock>()!;
    }
}
