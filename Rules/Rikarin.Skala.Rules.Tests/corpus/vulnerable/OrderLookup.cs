using System.Data;
using System.Net;

namespace Corpus.Vulnerable;

/// <summary>SK5001 — four spellings of the same mistake.</summary>
public static class OrderLookup {
    public static void ByConcatenation(HttpListenerRequest request, IDbCommand command) {
        command.CommandText = "select * from orders where reference = '" + request.QueryString["ref"] + "'";
    }

    public static void ByInterpolation(HttpListenerRequest request, IDbCommand command) {
        command.CommandText = $"select * from orders where reference = '{request.QueryString["ref"]}'";
    }

    public static void ThroughALocal(HttpListenerRequest request, IDbCommand command) {
        var reference = request.QueryString["ref"];
        var clause = "reference = '" + reference + "'";
        command.CommandText = "select * from orders where " + clause;
    }

    public static void ThroughACompoundAssignment(HttpListenerRequest request, IDbCommand command) {
        var sql = "select * from orders where reference = '";
        sql += request.QueryString["ref"];
        sql += "'";
        command.CommandText = sql;
    }
}
