using Text = System.String;

// `nameof(Text)` is "Text"; `typeof(Text).Name` is "String".
public sealed class Aliases {
    public string TypeName() => typeof(Text).Name;
}
