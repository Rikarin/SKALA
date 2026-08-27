using System.Data;
using System.Net;
using System.Text;

namespace Corpus.Vulnerable;

/// <summary>SK5001 — a builder, a loop, and a header rather than a query string.</summary>
public static class AuditTrail {
    public static void Search(HttpListenerRequest request, IDbCommand command) {
        var builder = new StringBuilder("select * from audit where 1 = 1");
        foreach (var actor in request.QueryString.GetValues("actor") ?? []) {
            builder.Append(" or actor = '").Append(actor).Append('\'');
        }

        command.CommandText = builder.ToString();
    }

    public static void ByTenantHeader(HttpListenerRequest request, IDbCommand command) {
        command.CommandText = "select * from audit where tenant = '" + request.Headers["X-Tenant"] + "'";
    }
}
