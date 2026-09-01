using System.Text;

sealed class Formatter {
    StringBuilder builder = new();

    public override string ToString() => builder.ToString();
}
