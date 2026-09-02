public sealed class Appending {
    // ⚠ The strongest case for the move being sound: after it the `return` calls a local function
    // declared below it, which binds because local functions are hoisted.
    public static int Run() {
        int Work() => 7;

        return Work();
    }
}
