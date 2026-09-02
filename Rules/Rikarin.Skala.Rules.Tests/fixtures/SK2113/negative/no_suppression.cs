// GetService used as designed: the null is handled rather than asserted away.
namespace Microsoft.Extensions.DependencyInjection {
    static class ServiceProviderServiceExtensions {
        public static T? GetService<T>(this System.IServiceProvider provider) => default;

        public static T GetRequiredService<T>(this System.IServiceProvider provider) where T : notnull =>
            throw new System.InvalidOperationException();

        public static T? GetKeyedService<T>(this System.IServiceProvider provider, object? key) => default;

        public static T GetRequiredKeyedService<T>(this System.IServiceProvider provider, object? key)
            where T : notnull => throw new System.InvalidOperationException();
    }
}

namespace Fixtures {
    using Microsoft.Extensions.DependencyInjection;

    interface IClock { }

    sealed class Host {
        public IClock? Resolve(System.IServiceProvider provider) => provider.GetService<IClock>();
    }
}
