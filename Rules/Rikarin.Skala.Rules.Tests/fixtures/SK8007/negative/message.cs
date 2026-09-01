using System; using Xunit; class C { [Fact] public void M() { Assert.True(true, DateTime.Now.ToString()); } }
