using System;

sealed class Descriptor {
    public Type Contract { get; set; } = typeof(object);

    public string Describe() => Contract.GetType().FullName ?? string.Empty;
}
