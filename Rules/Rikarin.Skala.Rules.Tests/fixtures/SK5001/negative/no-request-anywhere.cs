using System.Data;

public static class Health {
    public static void Check(IDbCommand command) {
        command.CommandText = "select 1";
        command.ExecuteScalar();
    }
}
