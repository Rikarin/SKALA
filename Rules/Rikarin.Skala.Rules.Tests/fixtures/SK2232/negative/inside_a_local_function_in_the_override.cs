using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    protected override Assembly Load(AssemblyName name) {
        return Open(name.Name + ".dll");

        static Assembly Open(string path) => Assembly.LoadFrom(path);
    }
}
