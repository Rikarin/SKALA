using System; class C { static bool Policy => false; static Lazy<int> Value = new(() => 1, Policy); }
