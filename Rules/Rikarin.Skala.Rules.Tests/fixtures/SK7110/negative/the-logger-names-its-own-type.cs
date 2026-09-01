// The repair, which must not still be reported.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class OrderService {
        readonly ILogger<OrderService> logger = null!;

        public override string ToString() => logger.ToString()!;
    }
}
