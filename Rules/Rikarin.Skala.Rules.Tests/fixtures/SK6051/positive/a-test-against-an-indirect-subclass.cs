namespace Contoso.Design;

// Two links down the chain is the same inversion: `Root` is edited when `Special` appears.
public class Root {
    public bool IsSpecial() => this is Special;
}

public class Middle : Root;

public sealed class Special : Middle;
