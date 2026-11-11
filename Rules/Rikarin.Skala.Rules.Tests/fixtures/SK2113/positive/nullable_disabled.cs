// ⚠ The rule reads the `!` token and the callee's containing type, never a flow state, so a
// nullable-oblivious file is reported identically — which is where the mistake is most likely and
// the compiler says least.
using Microsoft.Extensions.DependencyInjection;

namespace Fixtures {
    interface IClock { }

#nullable disable
    sealed class Host {
        public IClock Resolve(System.IServiceProvider provider) => provider.GetService<IClock>()!;
    }
#nullable restore
}
