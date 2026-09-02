using System.Threading.Tasks;

// The same `Task.Run(() => Drain())` as `positive/a-task-started-on-a-closure.cs`, moved one member
// down. The object is finished before anybody can call `Begin`, so the pool thread sees a complete
// object and there is no race to report. The rule's scope — one constructor declaration — is what
// buys this, and it is the reason the concept is "publishes before finishing" rather than "hands a
// delegate to a thread".
public sealed class Pump {
    readonly int[] buffer;

    public Pump(int size) => buffer = new int[size];

    public void Begin() => Task.Run(() => Drain());

    void Drain() {
        for (var i = 0; i < buffer.Length; i++) {
            buffer[i] = 0;
        }
    }
}
