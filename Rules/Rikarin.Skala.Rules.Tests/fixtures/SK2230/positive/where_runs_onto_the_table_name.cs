public sealed class Queries {
    public string ByActive() =>
        "select id, name from users"
        + "where active = 1";
}
