using System; class C { [ThreadStatic] static Lazy<int> Value = new(() => 1, false); }
