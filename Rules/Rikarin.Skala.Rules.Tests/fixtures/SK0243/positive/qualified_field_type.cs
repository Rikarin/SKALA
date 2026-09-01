using System.Text;

sealed class Formatter {
    System.Text.StringBuilder builder = new();

    public override string ToString() => builder.ToString();
}
