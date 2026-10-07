using System;

public class Named {
    public virtual string Name => "base";
}

public sealed class Counter : Named {
    readonly int start = 1;

    public override string Name => "derived";

    public Func<int, string> Build() {
        return delegate(int n) {
            var total = start;
        again:
            total += n;
            if (total < 10) {
                goto again;
            }

            return base.Name + this.start + total;
        };
    }
}
