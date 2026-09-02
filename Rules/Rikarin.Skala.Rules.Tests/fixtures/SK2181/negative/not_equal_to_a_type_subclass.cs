using System;
using System.Reflection;

static class Emit {
    public static bool IsRuntime(Type candidate) => candidate.GetType() != typeof(TypeDelegator);
}
