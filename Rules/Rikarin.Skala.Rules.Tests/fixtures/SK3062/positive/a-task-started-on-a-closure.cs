using System.Threading.Tasks;

// Shape D. The lambda names `Drain`, an instance method, so the closure carries `this` — and the
// pool thread can be inside `Drain` reading `buffer` before the line below it has assigned `buffer`.
// This is the failure that never reproduces on x64 and is legal on ARM64.
public sealed class Pump {
    readonly int[] buffer;

    public Pump(int size) {
        Task.Run(() => Drain());
        buffer = new int[size];
    }

    public int Depth => buffer.Length;

    void Drain() {
        for (var i = 0; i < buffer.Length; i++) {
            buffer[i] = 0;
        }
    }
}
