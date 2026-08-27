using System.Data;
using System.Net;

// `int.Parse` is a declared sanitizer: whatever it returns is an `int`, and an `int` cannot carry a
// quote into the statement however hostile its input was.
public static class Orders {
    public static void Load(HttpListenerRequest request, IDbCommand command) {
        var id = int.Parse(request.QueryString["id"]!);
        command.CommandText = "select * from orders where id = " + id;
    }
}
