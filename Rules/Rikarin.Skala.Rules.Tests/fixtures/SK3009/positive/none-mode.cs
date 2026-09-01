using System; using System.Threading; class C { static readonly Lazy<int> Value = new(() => 1, LazyThreadSafetyMode.None); }
