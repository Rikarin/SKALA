// The real `ServiceProviderServiceExtensions`: since .NET 11 the DI abstractions ship in
// Microsoft.NETCore.App, so a stub of them here would be ambiguous with the framework (CS0121).
using Microsoft.Extensions.DependencyInjection;

namespace Fixtures {
    interface IClock { }

    sealed class Host {
        public IClock Resolve(System.IServiceProvider provider) => provider.GetService<IClock>()!;
    }
}
