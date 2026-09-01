using System; using Xunit; class C { static void Equal(object expected, object actual) { } [Fact] public void M() { Equal(DateTime.MinValue, DateTime.Now); } }
