// The anti-vacuity fixture: `ILogger` carries no type argument at all.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class OrderService {
        readonly ILogger logger = null!;

        public override string ToString() => logger.ToString()!;
    }
}
