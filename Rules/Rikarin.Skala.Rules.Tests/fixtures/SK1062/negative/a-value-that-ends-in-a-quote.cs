// ⚠ The fence is greedy, so a content ending in a quote cannot be written on one line at all.
public sealed class Quoted {
    public string Said() => "say \"hi\"";
}
