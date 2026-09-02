// `GetCustomAttribute(member, typeof(Attribute))` means "any attribute" and is exactly right. The
// test is derives-from-*or-equals*, which is the whole difference.
using System;
using System.Reflection;

public sealed class Registry {
    public Attribute? Any(MemberInfo member) => Attribute.GetCustomAttribute(member, typeof(Attribute));
}
