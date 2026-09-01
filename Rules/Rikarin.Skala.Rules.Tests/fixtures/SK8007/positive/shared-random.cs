using System; using Xunit; class C { [Fact] public void M() { Assert.InRange(Random.Shared.Next(), 0, 100); } }
