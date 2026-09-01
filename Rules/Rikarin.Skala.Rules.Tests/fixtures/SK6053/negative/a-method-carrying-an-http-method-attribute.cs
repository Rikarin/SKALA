using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Mvc.Routing {
    public abstract class HttpMethodAttribute : System.Attribute;
}

namespace Microsoft.AspNetCore.Mvc {
    public sealed class HttpGetAttribute : Routing.HttpMethodAttribute;
}

namespace Contoso.Design {
    // The routing attribute alone is enough, without a controller base: it says the framework calls
    // this by name. The attribute is matched by walking its base chain, so a repository's own
    // attribute deriving from `HttpMethodAttribute` is covered too.
    public sealed class Handlers {
        [Microsoft.AspNetCore.Mvc.HttpGet]
        public Task<int> Get(int id) => Task.FromResult(id);
    }
}
