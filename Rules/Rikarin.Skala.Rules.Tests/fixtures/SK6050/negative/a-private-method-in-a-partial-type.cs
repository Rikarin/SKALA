namespace Contoso.Design;

// ⚠ The method-group scan reads one file, and that is sound only because a private member of a type
// declared in one place has all of its references there. A `partial` type has another half this
// compilation may not even contain, so the finding is withdrawn rather than made on a partial view.
public partial class Session {
    public int Timeout(string host) => Resolve(host);
}

public partial class Session {
    static int Resolve(string host) => 30;
}
