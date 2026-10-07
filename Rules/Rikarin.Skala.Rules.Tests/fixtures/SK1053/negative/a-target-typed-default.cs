// `_ = default;` is CS8716 — but `default` has no effect to keep, so the rule never proposes it.
public sealed class Pool {
    public void Warm() {
        int size = default;
    }
}
