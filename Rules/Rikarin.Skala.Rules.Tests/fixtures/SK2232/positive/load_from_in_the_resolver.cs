using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    readonly string directory;

    public PluginContext(string directory) => this.directory = directory;

    protected override Assembly Load(AssemblyName name) =>
        Assembly.LoadFrom(directory + "/" + name.Name + ".dll");
}
