using System;
using System.Reflection;

public sealed class Registry {
    public Delegate Bind(MethodInfo method) => Delegate.CreateDelegate(typeof(Action), method);
}
