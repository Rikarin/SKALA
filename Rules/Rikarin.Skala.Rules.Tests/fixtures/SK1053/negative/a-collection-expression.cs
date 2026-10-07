using System.Collections.Generic;

// `_ = [];` is CS9176: a collection expression has no natural type. It is not an effect either.
public sealed class Pool {
    public void Warm() {
        List<int> buffer = [];
    }
}
