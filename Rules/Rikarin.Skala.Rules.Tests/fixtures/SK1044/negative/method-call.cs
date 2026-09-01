public sealed class Calling {
    static string Read() => "value";

    // The long form calls `Read()` twice and `string.IsNullOrEmpty(Read())` calls it once. That is
    // an improvement and it is still a different program.
    public static bool IsBlank() => Read() == null || Read().Length == 0;
}
