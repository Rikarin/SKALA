using System;
using System.Reflection;

static class Emit {
    public static bool IsBuilt(Type candidate) => candidate.GetType() is TypeDelegator;
}
