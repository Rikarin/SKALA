public sealed class Queries {
    public string Settings() =>
        "select * from config where scope = 'off"
        + "setting'";
}
