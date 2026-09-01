using System; using Xunit; class C { [Fact] public void M() { Assert.Equal(DateTime.MinValue, DateTime.MinValue); } }
