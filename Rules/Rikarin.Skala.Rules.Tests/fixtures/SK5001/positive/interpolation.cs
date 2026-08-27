using System.Data;
using System.Net;

// Interpolation is concatenation with nicer syntax, and the database cannot tell the difference.
public static class Search {
    public static void Run(HttpListenerRequest request, IDbCommand command) {
        var term = request.QueryString["q"];
        command.CommandText = $"select * from items where name like '%{term}%'";
    }
}
