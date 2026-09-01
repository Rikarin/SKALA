// ⚠ `ILogger<T>` in a generic helper is a category decided at the use site, not a wrong one,
// and there is no name a fix could write in its place.
namespace Microsoft.Extensions.Logging {
    interface ILogger { }

    interface ILogger<out TCategoryName> : ILogger { }
}

namespace Fixtures {
    using Microsoft.Extensions.Logging;

    sealed class Handler<T> {
        readonly ILogger<T> logger = null!;

        public override string ToString() => logger.ToString()!;
    }
}
