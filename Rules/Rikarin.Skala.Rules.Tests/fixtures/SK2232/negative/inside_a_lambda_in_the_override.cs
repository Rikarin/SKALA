using System;
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    protected override Assembly Load(AssemblyName name) {
        Func<string, Assembly> open = static path => Assembly.LoadFrom(path);
        return open(name.Name + ".dll");
    }
}
