/// <summary>Writes <c>@class</c> and @event into the log; see the @remarks convention.</summary>
class C {
    // A local named @class would be reported; naming one in a comment is not a declaration.
    public int Write() => 0;
}
