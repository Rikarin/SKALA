using System; class C { static readonly Lazy<object> Value = new(() => new object()); }
