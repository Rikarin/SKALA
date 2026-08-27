using System;
using System.Data;
using System.Net;

public static class Accounts {
    public static void Load(HttpListenerRequest request, IDbCommand command) {
        var account = Guid.Parse(request.QueryString["account"]!);
        command.CommandText = $"select * from accounts where id = '{account}'";
    }
}
