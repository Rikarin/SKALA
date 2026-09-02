// ⚠ #302's shape (#325). The guard asked over the object creation's FULL span, which begins after
// the `=>`, so the sentence naming which member changes silenced the rule. The fix rewrites only
// the construction into a `with` expression.
public sealed record Settings(string Host, int Port, int Retries);

public sealed class Builder {
    public Settings WithRetries(Settings settings, int retries) =>
        // only the retry count changes; everything else is carried over verbatim
        new Settings(settings.Host, settings.Port, retries);
}
