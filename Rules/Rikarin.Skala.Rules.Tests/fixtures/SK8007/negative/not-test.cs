using System; using Xunit; class C { public void M() { Assert.NotEqual(Guid.Empty, Guid.NewGuid()); } }
