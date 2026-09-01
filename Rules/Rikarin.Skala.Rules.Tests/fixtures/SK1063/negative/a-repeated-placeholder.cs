// The interpolated form would evaluate `name` twice where `Format` evaluated it once.
public sealed class Echo {
    public string Twice(string name) => string.Format("{0} and {0}", name);
}
