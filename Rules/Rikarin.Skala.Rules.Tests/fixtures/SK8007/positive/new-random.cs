using System; using Xunit; class C { [Theory] public void M(int expected) { Assert.Equal(expected, new Random().Next()); } }
