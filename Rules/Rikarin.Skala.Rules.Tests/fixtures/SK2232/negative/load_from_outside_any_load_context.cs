using System.Reflection;

public sealed class Plugins {
    public Assembly Open(string path) => Assembly.LoadFrom(path);
}
