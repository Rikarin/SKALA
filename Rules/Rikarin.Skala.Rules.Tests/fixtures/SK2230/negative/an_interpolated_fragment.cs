public sealed class Queries {
    public string ByTable(string table) =>
        $"select * from {table}"
        + "where id = 1";
}
