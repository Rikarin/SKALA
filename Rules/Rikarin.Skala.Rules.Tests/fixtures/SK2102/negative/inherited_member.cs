using System.Diagnostics;

abstract class Shape {
    protected string label = "shape";
}

[DebuggerDisplay("{label,nq}")]
sealed class Circle : Shape { }
