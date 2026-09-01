using System; class FactAttribute : Attribute { } class C { [Fact] public void M() { Xunit.Assert.NotEqual(Guid.Empty, Guid.NewGuid()); } }
