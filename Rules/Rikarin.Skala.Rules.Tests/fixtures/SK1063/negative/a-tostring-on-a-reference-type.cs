// ⚠ On a reference type `null.ToString()` throws where `$"{null}"` renders the empty string.
// Removing the call is a behaviour change, not a modernization — and the nullable annotation is a
// promise, not a proof.
public sealed class Rendering {
    public string Label(object value) => $"{value.ToString()} left";
}
