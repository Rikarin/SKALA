namespace Fixture.Own;

// Somebody's own `Task`, in their own namespace, running the delegate inline on the calling thread.
// ⚠ The four schedulers are resolved from the compilation and never matched on the written name:
// `Task` and `Thread` are both plausible names for a domain type, and a finding here would send a
// reader hunting for concurrency that the program does not contain.
public static class Task {
    public static void Run(System.Action work) => work();
}

public sealed class Recipe {
    readonly string[] steps;

    public Recipe(int count) {
        steps = new string[count];
        Task.Run(() => Describe());
    }

    public int Steps => steps.Length;

    void Describe() {
        for (var i = 0; i < steps.Length; i++) {
            steps[i] = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
