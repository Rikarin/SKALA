// ⚠ A composition root names other classes` loggers on purpose. Reporting a local, a
// `typeof` or a factory return type would turn this into a rule against dependency injection.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class PaymentService { }

    sealed class Container {
        public System.Type Registration => typeof(ILogger<PaymentService>);

        public ILogger<PaymentService> Create() {
            ILogger<PaymentService> logger = null!;
            return logger;
        }
    }
}
