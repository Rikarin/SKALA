using System; using Xunit; class C { [Fact] public void M() { Assert.True(DateTimeOffset.Now.Year > 2000); } }
