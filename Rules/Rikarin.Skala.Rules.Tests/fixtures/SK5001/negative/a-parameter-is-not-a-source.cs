using System.Data;

// ⚠ The exclusion that decides whether this rule is usable at all. `sql` arrives from somewhere
// this method cannot see, so calling it tainted would be asserting a vulnerability rather than
// finding one — and the callers of a helper like this normally pass a constant with placeholders.
public static class Database {
    public static void Execute(IDbCommand command, string sql) {
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
