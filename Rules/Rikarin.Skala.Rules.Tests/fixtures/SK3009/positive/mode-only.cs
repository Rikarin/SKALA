using System; using System.Threading; class C { static Lazy<object> Value = new(LazyThreadSafetyMode.None); }
