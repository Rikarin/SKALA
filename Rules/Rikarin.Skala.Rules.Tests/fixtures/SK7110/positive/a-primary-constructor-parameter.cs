// A primary-constructor parameter has no constructor body to sit in; the enclosing type is
// found from the type declaration, which is why this shape is reported at all.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class PaymentService { }

    sealed class OrderService(ILogger<PaymentService> logger) {
        public override string ToString() => logger.ToString()!;
    }
}
