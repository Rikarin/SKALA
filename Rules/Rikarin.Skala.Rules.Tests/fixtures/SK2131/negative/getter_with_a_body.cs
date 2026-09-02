// ⚠ Added because a sabotage stayed green. `expression_bodied.cs` covers `=> 1024` on the
// *property*, which a different guard declines; nothing covered a `get` with a block body, so
// removing the auto-property shape test changed no fixture at all. A getter that computes has no
// storage to be left at `default`.
sealed class Window {
    public int Width {
        get { return 1024; }
    }

    public bool IsWide => Width > 800;
}
