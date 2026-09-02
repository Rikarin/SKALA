using System;
using System.Reflection;

public sealed class Marker { }

public sealed class Registry {
    public Attribute? Read(MemberInfo member) => Attribute.GetCustomAttribute(member, typeof(Marker));
}
