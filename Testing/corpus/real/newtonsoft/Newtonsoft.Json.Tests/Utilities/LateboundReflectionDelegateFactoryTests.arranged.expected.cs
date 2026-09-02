// skala-oracle: resharper=2025.2.6 config=sha256:e256d0b9ed35b14f profile=SkalaCleanup generated=2026-09-02
using System.Reflection;
#if DNXCORE50
using Xunit;
using Test = Xunit.FactAttribute;
using Assert = Newtonsoft.Json.Tests.XUnitAssert;
#else
#endif
#if NET20
using Newtonsoft.Json.Utilities.LinqBridge;
#else

#endif

namespace Newtonsoft.Json.Tests.Utilities;

public class OutAndRefTestClass {
    public string Input { get; set; }
    public bool B1 { get; set; }
    public bool B2 { get; set; }

    public OutAndRefTestClass(ref string value) {
        Input = value;
        value = "Output";
    }

    public OutAndRefTestClass(ref string value, out bool b1)
        : this(ref value) {
        b1 = true;
        B1 = true;
    }

    public OutAndRefTestClass(ref string value, ref bool b1, ref bool b2)
        : this(ref value) {
        B1 = b1;
        B2 = b2;
    }
}

public class InTestClass {
    public string Value { get; }
    public bool B1 { get; }

    public InTestClass(in string value) {
        Value = value;
    }

    public InTestClass(in string value, in bool b1)
        : this(in value) {
        B1 = b1;
    }
}

[TestFixture]
public class LateboundReflectionDelegateFactoryTests : TestFixtureBase {
    [Test]
    public void ConstructorWithInString() {
        ConstructorInfo constructor = TestReflectionUtils.GetConstructors(typeof(InTestClass))
            .Single(c => c.GetParameters().Count() == 1);

        var creator = LateBoundReflectionDelegateFactory.Instance.CreateParameterizedConstructor(constructor);

        var args = new object[] { "Value" };
        var o = (InTestClass)creator(args);
        Assert.IsNotNull(o);
        Assert.AreEqual("Value", o.Value);
    }

    [Test]
    public void ConstructorWithInStringAndBool() {
        ConstructorInfo constructor = TestReflectionUtils.GetConstructors(typeof(InTestClass))
            .Single(c => c.GetParameters().Count() == 2);

        var creator = LateBoundReflectionDelegateFactory.Instance.CreateParameterizedConstructor(constructor);

        var args = new object[] { "Value", true };
        var o = (InTestClass)creator(args);
        Assert.IsNotNull(o);
        Assert.AreEqual("Value", o.Value);
        Assert.AreEqual(true, o.B1);
    }

    [Test]
    public void ConstructorWithRefString() {
        ConstructorInfo constructor = TestReflectionUtils.GetConstructors(typeof(OutAndRefTestClass))
            .Single(c => c.GetParameters().Count() == 1);

        var creator = LateBoundReflectionDelegateFactory.Instance.CreateParameterizedConstructor(constructor);

        var args = new object[] { "Input" };
        var o = (OutAndRefTestClass)creator(args);
        Assert.IsNotNull(o);
        Assert.AreEqual("Input", o.Input);
    }

    [Test]
    public void ConstructorWithRefStringAndOutBool() {
        ConstructorInfo constructor = TestReflectionUtils.GetConstructors(typeof(OutAndRefTestClass))
            .Single(c => c.GetParameters().Count() == 2);

        var creator = LateBoundReflectionDelegateFactory.Instance.CreateParameterizedConstructor(constructor);

        var args = new object[] { "Input", null };
        var o = (OutAndRefTestClass)creator(args);
        Assert.IsNotNull(o);
        Assert.AreEqual("Input", o.Input);
    }

    [Test]
    public void ConstructorWithRefStringAndRefBoolAndRefBool() {
        ConstructorInfo constructor = TestReflectionUtils.GetConstructors(typeof(OutAndRefTestClass))
            .Single(c => c.GetParameters().Count() == 3);

        var creator = LateBoundReflectionDelegateFactory.Instance.CreateParameterizedConstructor(constructor);

        var args = new object[] { "Input", true, null };
        var o = (OutAndRefTestClass)creator(args);
        Assert.IsNotNull(o);
        Assert.AreEqual("Input", o.Input);
        Assert.AreEqual(true, o.B1);
        Assert.AreEqual(false, o.B2);
    }
}

public struct MyStruct {
    int _intProperty;

    public int IntProperty {
        get => _intProperty;
        set => _intProperty = value;
    }
}
