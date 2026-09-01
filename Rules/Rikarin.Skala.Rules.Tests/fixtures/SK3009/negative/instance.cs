using System; class C { readonly Lazy<int> value = new(() => 1, false); }
