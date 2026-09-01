using System; class C { static Lazy<int> Value = Create(); static Lazy<int> Create() => new(() => 1, false); }
