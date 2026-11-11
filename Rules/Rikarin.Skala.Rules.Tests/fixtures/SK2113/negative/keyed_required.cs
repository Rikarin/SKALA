using Microsoft.Extensions.DependencyInjection;

namespace Fixtures {
    interface IClock { }

    sealed class Host {
        public IClock Resolve(System.IServiceProvider provider) =>
            provider.GetRequiredKeyedService<IClock>("main");
    }
}
