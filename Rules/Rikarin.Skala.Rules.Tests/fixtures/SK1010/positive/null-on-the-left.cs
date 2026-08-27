using System.Text;

public sealed class Holder {
    public static bool Missing(StringBuilder builder) => null == builder;
}
