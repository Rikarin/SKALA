// `public` on a member of a type nothing outside the assembly can see is not public API. The chain
// is what decides, not the modifier on the declaration.
sealed class Archetype {
    public void Clear() { }
}
