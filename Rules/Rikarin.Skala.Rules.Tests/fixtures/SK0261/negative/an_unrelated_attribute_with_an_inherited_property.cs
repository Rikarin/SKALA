using System;

sealed class RouteAttribute : Attribute {
    public RouteAttribute(string template) { }

    public bool Inherited { get; set; }
}

[Route("/api", Inherited = true)]
class C { }
