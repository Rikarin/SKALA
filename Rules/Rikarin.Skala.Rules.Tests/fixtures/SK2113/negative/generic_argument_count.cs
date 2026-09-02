// A same-named extension in the right namespace but with two type arguments is not the method the
// fix knows how to rename.
namespace Microsoft.Extensions.DependencyInjection {
    static class ServiceProviderServiceExtensions {
        public static TResult? GetService<TService, TResult>(this System.IServiceProvider provider) => default;
    }
}

namespace Fixtures {
    using Microsoft.Extensions.DependencyInjection;

    interface IClock { }

    sealed class Host {
        public IClock Resolve(System.IServiceProvider provider) => provider.GetService<object, IClock>()!;
    }
}
