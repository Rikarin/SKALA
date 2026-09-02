public sealed class Queries {
    public string Newest() =>
        "select id from events where kind = 3"
        + "order by created desc";
}
