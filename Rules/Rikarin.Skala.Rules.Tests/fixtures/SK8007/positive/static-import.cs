using static System.DateTime; using Xunit; class C { [Fact] public void M() { Assert.Equal(MinValue, UtcNow); } }
