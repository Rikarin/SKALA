using System;
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    protected override IntPtr LoadUnmanagedDll(string name) {
        var probe = Assembly.LoadFrom(name + ".dll");
        return probe is null ? IntPtr.Zero : IntPtr.Zero;
    }
}
