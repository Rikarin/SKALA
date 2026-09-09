using System.Text;

public static class Report {
    // `StringBuilder.Append(ref AppendInterpolatedStringHandler)`. The exclusion is not
    // `string.Create`-shaped: the same conversion sits on every handler target.
    public static string Describe(double bleed) {
        var builder = new StringBuilder();
        builder.Append($"the bleed is {bleed:F1} kg/s " + $"and that is all of it");
        return builder.ToString();
    }
}
