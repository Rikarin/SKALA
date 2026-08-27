using System.Data;
using System.Net;
using System.Text;

namespace Corpus.Safe;

/// <summary>
/// SK5001's twin. ⚠ The builder and the loop are still here — the difference is that what goes
/// into the builder is a placeholder and what goes into the command is a bound parameter.
/// </summary>
public static class AuditTrail {
    public static void Search(HttpListenerRequest request, IDbCommand command) {
        var builder = new StringBuilder("select * from audit where 1 = 1");
        var index = 0;
        foreach (var actor in request.QueryString.GetValues("actor") ?? []) {
            var name = "@actor" + index++;
            builder.Append(" or actor = ").Append(name);
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = actor;
            command.Parameters.Add(parameter);
        }

        command.CommandText = builder.ToString();
    }

    public static void ByTenantHeader(HttpListenerRequest request, IDbCommand command) {
        command.CommandText = "select * from audit where tenant = @tenant";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@tenant";
        parameter.Value = request.Headers["X-Tenant"];
        command.Parameters.Add(parameter);
    }
}
