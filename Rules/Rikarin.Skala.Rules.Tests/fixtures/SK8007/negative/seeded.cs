using System; using Xunit; class C { [Fact] public void M() { Assert.InRange(new Random(42).Next(), 0, int.MaxValue); } }
