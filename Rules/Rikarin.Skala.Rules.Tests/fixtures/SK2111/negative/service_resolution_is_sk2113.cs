// ⚠ The crossing fixture with SK2113. This `!` is not inert — it is the wrong answer to a real
// warning — and the two rules are negations of one predicate, so neither can report it twice.
#nullable enable
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
#nullable disable
        public IClock Resolve(System.IServiceProvider provider) => provider.GetService<IClock>()!;
#nullable restore
    }
}
