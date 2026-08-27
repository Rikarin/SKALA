using System.Data;
using System.Net;

// The fix, and therefore the most important thing the rule must stay silent on.
public static class Orders {
    public static void Load(HttpListenerRequest request, IDbCommand command) {
        command.CommandText = "select * from orders where id = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@id";
        parameter.Value = request.QueryString["id"];
        command.Parameters.Add(parameter);
    }
}
