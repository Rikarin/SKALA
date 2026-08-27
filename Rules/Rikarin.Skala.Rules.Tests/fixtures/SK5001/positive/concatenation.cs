using System.Data;
using System.Net;

// The shape the rule exists for: a value off the request object, glued into the text with `+`.
public static class Orders {
    public static void Load(HttpListenerRequest request, IDbCommand command) {
        var id = request.QueryString["id"];
        command.CommandText = "select * from orders where id = '" + id + "'";
    }
}
