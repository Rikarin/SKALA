using System.Data;

namespace Corpus.Safe;

/// <summary>
///     ⚠ The helper shape. `sql` arrives from callers this method cannot see, and the rule must be
///     silent: an intra-procedural engine that called an unknown parameter tainted would report every
///     data-access layer ever written.
/// </summary>
public static class Database {
    public static int Execute(IDbConnection connection, string sql, params (string Name, object Value)[] arguments) {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in arguments) {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command.ExecuteNonQuery();
    }
}
