public sealed class Queries {
    public string Update() =>
        "update accounts "
        + "set balance = 0"
        + "where id = 7";
}
