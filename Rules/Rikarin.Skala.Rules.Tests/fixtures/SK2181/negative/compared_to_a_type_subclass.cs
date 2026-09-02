using System;
using System.Reflection;

static class Emit {
    // Reflection-emit code separating a runtime type from a built one. The author means it.
    public static bool IsBuilt(Type candidate) => candidate.GetType() == typeof(TypeDelegator);
}
