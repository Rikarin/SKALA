// ⚠ A hierarchy that files its whole family under the base class`s category is a decision,
// and the type argument still names something whose declaration the reader can find.
namespace Fixtures {
    using Microsoft.Extensions.Logging;

    abstract class Service { }

    abstract class HttpService : Service { }

    sealed class OrderService : HttpService {
        readonly ILogger<Service> logger = null!;

        public override string ToString() => logger.ToString()!;
    }
}
