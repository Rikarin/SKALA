// ⚠ The crossing fixture with SK2113. This `!` is not inert — it is the wrong answer to a real
// warning — and the two rules are negations of one predicate, so neither can report it twice.
#nullable enable
using Microsoft.Extensions.DependencyInjection;

namespace Fixtures {
    interface IClock { }

    sealed class Host {
#nullable disable
        public IClock Resolve(System.IServiceProvider provider) => provider.GetService<IClock>()!;
#nullable restore
    }
}
