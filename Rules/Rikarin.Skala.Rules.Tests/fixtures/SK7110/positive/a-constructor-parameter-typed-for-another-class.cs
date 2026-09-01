// The injection site is where the container decides the category, so this is where the
// mistake is usually made and where it is cheapest to catch.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class PaymentService { }

    sealed class OrderService {
        readonly ILogger logger;

        public OrderService(ILogger<PaymentService> logger) => this.logger = logger;

        public override string ToString() => logger.ToString()!;
    }
}
