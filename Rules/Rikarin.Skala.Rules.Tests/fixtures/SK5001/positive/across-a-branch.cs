using System.Data;
using System.Net;

// Tainted on one arm only. The merge is a union, so the value is tainted after the `if`.
public static class Filter {
    public static void Apply(HttpListenerRequest request, IDbCommand command, bool byName) {
        var clause = "1 = 1";
        if (byName) {
            clause = "name = '" + request.QueryString["name"] + "'";
        }

        command.CommandText = "select * from users where " + clause;
    }
}
