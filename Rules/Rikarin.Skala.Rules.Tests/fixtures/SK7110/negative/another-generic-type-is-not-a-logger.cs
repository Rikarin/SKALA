// Binding is on ILogger<> by metadata name, so an unrelated one-argument generic is silent.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class PaymentService { }

    sealed class Cache<T> { }

    sealed class OrderService {
        readonly Cache<PaymentService> cache = null!;

        public override string ToString() => cache.ToString()!;
    }
}
