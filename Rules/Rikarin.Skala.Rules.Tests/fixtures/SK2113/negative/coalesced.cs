using Microsoft.Extensions.DependencyInjection;

namespace Fixtures {
    interface IClock { }

    sealed class Fallback : IClock { }

    sealed class Host {
        public IClock Resolve(System.IServiceProvider provider) =>
            provider.GetService<IClock>() ?? new Fallback();
    }
}
