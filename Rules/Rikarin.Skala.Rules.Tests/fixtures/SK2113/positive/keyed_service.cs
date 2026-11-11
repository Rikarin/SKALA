// ⚠ The keyed resolution is declared on `ServiceProviderKeyedServiceExtensions`, not on
// `ServiceProviderServiceExtensions`. This fixture used to stub it on the wrong class, and so passed
// while the rule missed every real `GetKeyedService<T>(key)!`; it binds the framework's own now.
using Microsoft.Extensions.DependencyInjection;

namespace Fixtures {
    interface IClock { }

    sealed class Host {
        public IClock Resolve(System.IServiceProvider provider) => provider.GetKeyedService<IClock>("main")!;
    }
}
