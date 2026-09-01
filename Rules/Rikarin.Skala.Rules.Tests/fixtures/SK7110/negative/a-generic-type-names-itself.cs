// Comparison is on original definitions, so `Repository<T>` holding `ILogger<Repository<T>>`
// is correct rather than a near miss.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Repository<T> {
        readonly ILogger<Repository<T>> logger = null!;

        public override string ToString() => logger.ToString()!;
    }
}
