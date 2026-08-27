using System.Data;
using System.Net;

// The `out` value of a successful `TryParse` is an `int` that was never a string.
public static class Paging {
    public static void Load(HttpListenerRequest request, IDbCommand command) {
        if (!int.TryParse(request.QueryString["page"], out var page)) {
            page = 1;
        }

        command.CommandText = "select * from posts limit 20 offset " + page * 20;
    }
}
