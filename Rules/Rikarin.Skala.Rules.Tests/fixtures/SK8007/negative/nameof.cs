using System; using Xunit; class C { [Fact] public void M() { Assert.Equal("Now", nameof(DateTime.Now)); } }
