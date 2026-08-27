using System;
using System.Data;
using System.Net;

namespace Corpus.Safe;

/// <summary>SK5001's twin: the same four methods, parameterised or sanitised.</summary>
public static class OrderLookup {
    public static void Parameterised(HttpListenerRequest request, IDbCommand command) {
        command.CommandText = "select * from orders where reference = @reference";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@reference";
        parameter.Value = request.QueryString["ref"];
        command.Parameters.Add(parameter);
    }

    public static void ParsedToAnInteger(HttpListenerRequest request, IDbCommand command) {
        var id = int.Parse(request.QueryString["id"]!);
        command.CommandText = $"select * from orders where id = {id}";
    }

    public static void ParsedToAGuid(HttpListenerRequest request, IDbCommand command) {
        var account = Guid.Parse(request.QueryString["account"]!);
        command.CommandText = "select * from orders where account = '" + account + "'";
    }

    public static void AllOfTheHolesAreConstants(HttpListenerRequest request, IDbCommand command) {
        const string Table = "orders";
        const string Columns = "id, reference, total";
        command.CommandText = $"select {Columns} from {Table} where placed_at > now() - interval '1 day'";
    }
}
