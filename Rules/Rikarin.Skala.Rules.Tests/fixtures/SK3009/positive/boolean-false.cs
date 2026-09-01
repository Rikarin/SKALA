using System; class C { static readonly Lazy<object> Value = new Lazy<object>(() => new object(), false); }
