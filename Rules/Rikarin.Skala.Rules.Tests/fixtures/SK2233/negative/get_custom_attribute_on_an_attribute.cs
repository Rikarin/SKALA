using System;
using System.Reflection;

public sealed class MarkerAttribute : Attribute { }

public sealed class Registry {
    public Attribute? Read(MemberInfo member) => Attribute.GetCustomAttribute(member, typeof(MarkerAttribute));
}
