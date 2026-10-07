using System;
using System.Threading.Tasks;

public static class Loader {
    public static Func<int, Task<int>> Load() {
        return async delegate(int id) {
            await Task.Yield();
            return id;
        };
    }
}
