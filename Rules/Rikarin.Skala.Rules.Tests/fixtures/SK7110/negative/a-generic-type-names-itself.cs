// Comparison is on original definitions, so `Repository<T>` holding `ILogger<Repository<T>>`
// is correct rather than a near miss.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Repository<T> {
        readonly ILogger<Repository<T>> logger = null!;

        public override string ToString() => logger.ToString()!;
    }
}
