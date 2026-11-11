// GetService used as designed: the null is handled rather than asserted away.
using Microsoft.Extensions.DependencyInjection;

namespace Fixtures {
    interface IClock { }

    sealed class Host {
        public IClock? Resolve(System.IServiceProvider provider) => provider.GetService<IClock>();
    }
}
