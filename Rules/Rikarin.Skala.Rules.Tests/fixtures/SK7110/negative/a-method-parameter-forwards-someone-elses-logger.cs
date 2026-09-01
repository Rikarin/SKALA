// A method parameter is not a logger the type *keeps*; it is one passed through.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class PaymentService { }

    sealed class Wiring {
        public string Attach(ILogger<PaymentService> logger) => logger.ToString()!;
    }
}
