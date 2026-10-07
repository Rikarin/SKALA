// ⚠ #397: the injection site written as a C# 14 partial constructor. The parameter type is written
// on both halves and the two must agree, so the fix renames the category in both; renaming it in the
// implementation alone left two constructors, one with no definition and one with no implementation.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class PaymentService { }

    sealed partial class OrderService {
        readonly ILogger logger;

        public partial OrderService(ILogger<PaymentService> logger);

        public partial OrderService(ILogger<PaymentService> logger) => this.logger = logger;

        public override string ToString() => logger.ToString()!;
    }
}
