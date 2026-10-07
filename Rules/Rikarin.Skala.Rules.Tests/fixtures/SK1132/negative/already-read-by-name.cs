// The fix's own output: every element is read by its name.
public static class Inventory {
    public static string Use((int Count, string Name) r) => r.Name + ": " + r.Count;
}
