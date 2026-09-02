// Reordering would move the two calls past one another, which is a change to the program.
class SideEffects {
    public string Build() {
        var head = new { Id = Next(), Name = Label() };
        var tail = new { Name = Label(), Id = Next() };
        return head.ToString() + tail.ToString();
    }

    static int Next() => 1;

    static string Label() => "x";
}
