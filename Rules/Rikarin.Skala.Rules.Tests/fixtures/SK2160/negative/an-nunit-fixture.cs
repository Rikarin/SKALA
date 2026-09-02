using System;
using NUnit.Framework;

[TestFixture]
public sealed class ScheduleTests {
    [Test]
    public void Runs() {
        var now = DateTime.UtcNow;
        _ = now;
    }
}

namespace NUnit.Framework {
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TestFixtureAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Method)]
    public class TestAttribute : Attribute { }
}
