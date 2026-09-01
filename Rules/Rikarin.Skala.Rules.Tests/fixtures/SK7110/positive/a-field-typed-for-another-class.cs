// Every message OrderService writes is filed under PaymentService, so a filter that selects
// OrderService misses them and one that selects PaymentService collects what it never sent.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class PaymentService { }

    sealed class OrderService {
        readonly ILogger<PaymentService> logger = null!;

        public override string ToString() => logger.ToString()!;
    }
}
