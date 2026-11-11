// The repair, which must not still be reported.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class OrderService {
        readonly ILogger<OrderService> logger = null!;

        public override string ToString() => logger.ToString()!;
    }
}
