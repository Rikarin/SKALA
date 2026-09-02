public sealed class Naming {
    static string Build(int id) => id.ToString();

    public static int Describe(int id) {
        string name;
        name = Build(id);
        return name.Length;
    }
}
