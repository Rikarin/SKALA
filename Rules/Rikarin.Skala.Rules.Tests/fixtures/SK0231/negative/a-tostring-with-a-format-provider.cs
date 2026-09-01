using System.Globalization;

public static class Cultured {
    public static string Describe(string name) => name.ToString(CultureInfo.InvariantCulture);
}
