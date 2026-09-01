namespace Contoso.Design;

// A partial type's other part may declare a public constructor or create it, which is exactly where
// "the file holds every access to a private member" stops being true.
public sealed partial class Cursor {
    private Cursor() { }

    public int Position => 0;
}

// Already saying what the rule's advice says.
public static class Endpoints {
    public static string Health => "/health";
}
