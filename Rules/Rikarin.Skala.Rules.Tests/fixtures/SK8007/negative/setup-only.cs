using System; using Xunit; class C { [Fact] public void M() { var id = Guid.NewGuid(); Assert.NotEqual(Guid.Empty, id); } }
