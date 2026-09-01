using System.Text;

sealed class Formatter {
    System /* the framework one, not ours */.Text.StringBuilder builder = new();

    public override string ToString() => builder.ToString();
}
