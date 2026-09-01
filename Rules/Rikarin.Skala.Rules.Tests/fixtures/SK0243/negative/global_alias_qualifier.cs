using System.Text;

sealed class Formatter {
    global::System.Text.StringBuilder builder = new();

    public override string ToString() => builder.ToString();
}
