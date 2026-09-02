// The context declares this helper and never calls it from `Load`. Whether the default context is
// where that assembly belongs is not stated anywhere, so the rule does not decide it.
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    public Assembly OpenTool(string path) => Assembly.LoadFrom(path);

    protected override Assembly Load(AssemblyName name) => LoadFromAssemblyName(name);
}
