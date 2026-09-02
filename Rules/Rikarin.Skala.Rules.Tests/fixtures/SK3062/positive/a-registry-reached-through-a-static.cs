using System.Collections.Generic;

// Shape B. `Add` is an ordinary instance method, and that is not what decides the finding — the
// receiver is. `Registry.Instances` is static, so the list holds this reference for the life of the
// process, and it holds it from a point where `buffer` is still null.
public static class Registry {
    public static readonly List<Worker> Instances = new();
}

public sealed class Worker {
    readonly int[] buffer;

    public Worker(int size) {
        Registry.Instances.Add(this);
        buffer = new int[size];
    }

    public int Size => buffer.Length;
}
