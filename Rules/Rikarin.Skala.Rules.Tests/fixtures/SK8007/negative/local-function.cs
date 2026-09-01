using System; using Xunit; class C { [Fact] public void M() { void Check() { Assert.NotEqual(Guid.Empty, Guid.NewGuid()); } Check(); } }
