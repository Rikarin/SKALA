using System.Data;
using System.Net;
using System.Text;

// The flow is into the builder across several statements and out again through ToString().
public static class Report {
    public static void Build(HttpListenerRequest request, IDbCommand command) {
        var builder = new StringBuilder("select * from events where actor = '");
        builder.Append(request.QueryString["actor"]);
        builder.Append("'");
        command.CommandText = builder.ToString();
    }
}
