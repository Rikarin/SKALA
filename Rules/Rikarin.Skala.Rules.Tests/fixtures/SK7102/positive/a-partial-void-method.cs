// A `partial void` method returns nothing by the language's own rule.
public sealed partial class Hooks {
    /// <summary>Called after loading.</summary>
    /// <returns>Whether loading succeeded.</returns>
    partial void OnLoaded();
}
