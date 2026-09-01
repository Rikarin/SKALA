using System; using Xunit; class C { [Fact] public void M() { Assert.All(new[] { 1 }, x => Assert.NotEqual(Guid.Empty, Guid.NewGuid())); } }
