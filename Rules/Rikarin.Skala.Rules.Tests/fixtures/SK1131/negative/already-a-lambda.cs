using System;
using System.Threading.Tasks;

public static class Modern {
    public static readonly Func<int, int> Increment = x => x + 1;

    public static readonly Func<int, int, int> Add = (int a, int b) => { return a + b; };

    public static readonly Func<int, int> Twice = static x => x * 2;

    public static readonly Func<object?, EventArgs, Task> Handler = async (s, e) => { await Task.Yield(); };
}
