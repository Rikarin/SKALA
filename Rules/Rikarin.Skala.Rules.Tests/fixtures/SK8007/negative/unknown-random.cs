using System; using Xunit; class C { readonly Random random = new(42); [Fact] public void M() { Assert.InRange(random.Next(), 0, int.MaxValue); } }
