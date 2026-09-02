using System;
using System.Threading.Tasks;

// ⚠ This lambda really is `async void` — and it is reported once, at the conversion, by SK3052,
// which is where the remedy is. Reporting the throw as well would be two findings about one
// mistake with only one of them actionable.
public sealed class Panel {
    public void Wire() {
        Action callback = async () => {
            await Task.Yield();
            throw new InvalidOperationException("unobservable");
        };

        callback();
    }
}
