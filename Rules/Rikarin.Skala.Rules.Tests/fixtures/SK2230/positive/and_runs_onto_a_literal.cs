public sealed class Queries {
    public string Both() =>
        "select * from orders where paid = 1"
        + "and shipped = 0";
}
