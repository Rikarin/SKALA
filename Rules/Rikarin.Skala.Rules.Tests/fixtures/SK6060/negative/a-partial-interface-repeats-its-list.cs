// C# requires the variance modifiers to agree on every partial declaration, so an edit to one part
// is CS0264.
public partial interface ISplit<T> {
    T Create();
}

public partial interface ISplit<T> {
    T CreateAnother();
}
