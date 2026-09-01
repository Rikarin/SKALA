// A property keeps a logger the same way a field does.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class PaymentService { }

    sealed class OrderService {
        ILogger<PaymentService> Logger { get; init; } = null!;

        public override string ToString() => Logger.ToString()!;
    }
}
