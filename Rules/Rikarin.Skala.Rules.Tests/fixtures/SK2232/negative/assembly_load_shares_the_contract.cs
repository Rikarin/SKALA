// ⚠ The exclusion the rule exists for. Returning `Assembly.Load` from the override says "this
// dependency is shared -- take it from the default context", which is how a plugin and its host
// agree on a contract assembly. Contradicting that would be the false positive.
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    protected override Assembly Load(AssemblyName name) => Assembly.Load(name);
}
