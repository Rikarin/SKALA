using System.Threading.Tasks;

// ⚠ The framework is declared in source rather than referenced, which is what a fixture is: symbols are
// resolved by metadata name against the compilation, so a framework in source and a framework in an
// assembly resolve identically. A rule that additionally demanded metadata would be a rule whose
// ASP.NET fixtures proved nothing.
namespace Microsoft.AspNetCore.Mvc {
    public abstract class ControllerBase;

    public sealed class ApiControllerAttribute : System.Attribute;
}

namespace Contoso.Design {
    // An action is named by a routing convention. Adding the suffix changes the route, so the finding
    // would be advice that breaks the application.
    [Microsoft.AspNetCore.Mvc.ApiController]
    public sealed class OrdersController : Microsoft.AspNetCore.Mvc.ControllerBase {
        public Task<int> Get(int id) => Task.FromResult(id);
    }
}
