using System.Globalization;
using System.Text.RegularExpressions;

namespace Rikarin.Skala.Release;

/// <summary>How far a measured change moves the number.</summary>
/// <remarks>
/// ⚠ There is no <c>None</c>. docs/plan/18 § "The detectors": a release with no detected change is
/// still a release — different bytes, a different tool — so the floor is <see cref="Patch"/> and
/// "nothing changed" is a statement about the compatibility surface rather than about the artefact.
/// </remarks>
public enum BumpKind {
    Patch,
    Minor,
    Major
}

/// <summary>
/// A semantic version, with the pre-release identifier this project's release line depends on.
/// </summary>
/// <remarks>
/// ⚠ Build metadata (<c>+sha</c>) is parsed and discarded. NuGet strips it from the package version,
/// so a scheme that carried meaning in it would carry meaning that does not survive `dotnet pack`.
/// The commit is recorded in <c>SourceRevisionId</c> instead, which is where SourceLink looks.
/// </remarks>
public sealed partial record SemanticVersion(int Major, int Minor, int Patch, string? PreRelease)
    : IComparable<SemanticVersion> {
    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    /// <summary>The <c>alpha</c> of <c>1.0.0-alpha.7</c>, or null.</summary>
    public string? PreReleaseLabel {
        get {
            if (PreRelease is not { Length: > 0 } pre) {
                return null;
            }

            var dot = pre.IndexOf('.', StringComparison.Ordinal);
            return dot < 0 ? pre : pre[..dot];
        }
    }

    /// <summary>The <c>7</c> of <c>1.0.0-alpha.7</c>, or 0 when there is no counter.</summary>
    public int PreReleaseCounter {
        get {
            if (PreRelease is not { Length: > 0 } pre) {
                return 0;
            }

            var dot = pre.IndexOf('.', StringComparison.Ordinal);
            return dot >= 0
                && int.TryParse(pre[(dot + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                    ? n
                    : 0;
        }
    }

    public static SemanticVersion Parse(string text) =>
        TryParse(text, out var version)
            ? version
            : throw new FormatException($"'{text}' is not a semantic version.");

    public static bool TryParse(string? text, out SemanticVersion version) {
        version = new SemanticVersion(0, 0, 0, null);
        if (text is null) {
            return false;
        }

        var match = Pattern().Match(text.Trim().TrimStart('v', 'V'));
        if (!match.Success) {
            return false;
        }

        version = new SemanticVersion(
            int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture),
            int.Parse(match.Groups["patch"].Value, CultureInfo.InvariantCulture),
            match.Groups["pre"].Success ? match.Groups["pre"].Value : null
        );

        return true;
    }

    /// <summary>The release this one becomes when the measurement says <paramref name="bump"/>.</summary>
    /// <remarks>
    /// ⚠ A pre-release is <b>not</b> bumped by the verdict, and this is the whole of doc 18
    /// § "Why the first published artefact is a pre-release". <c>2.0.0-alpha.7</c> means 2.0.0 has
    /// not happened; nothing in the alpha series is a compatibility promise, so a major-classified
    /// change inside it advances the counter and is *recorded in the notes* rather than advancing
    /// the major. Advancing it would publish <c>3.0.0</c> before <c>2.0.0</c> existed.
    /// </remarks>
    public SemanticVersion Next(BumpKind bump) =>
        IsPreRelease
            ? this with { PreRelease = $"{PreReleaseLabel}.{PreReleaseCounter + 1}" }
            : bump switch {
                BumpKind.Major => new SemanticVersion(Major + 1, 0, 0, null),
                BumpKind.Minor => new SemanticVersion(Major, Minor + 1, 0, null),
                _ => new SemanticVersion(Major, Minor, Patch + 1, null)
            };

    /// <summary>The same release, as the Nth build of a pre-release series.</summary>
    public SemanticVersion AsPreRelease(string label, int counter) => this with { PreRelease = $"{label}.{counter}" };

    public int CompareTo(SemanticVersion? other) {
        if (other is null) {
            return 1;
        }

        if (Major != other.Major) {
            return Major.CompareTo(other.Major);
        }

        if (Minor != other.Minor) {
            return Minor.CompareTo(other.Minor);
        }

        if (Patch != other.Patch) {
            return Patch.CompareTo(other.Patch);
        }

        // SemVer §11: a version with a pre-release sorts *below* the same version without one.
        if (IsPreRelease != other.IsPreRelease) {
            return IsPreRelease ? -1 : 1;
        }

        if (!IsPreRelease) {
            return 0;
        }

        return ComparePreRelease(PreRelease!, other.PreRelease!);
    }

    static int ComparePreRelease(string left, string right) {
        var a = left.Split('.');
        var b = right.Split('.');

        for (var i = 0; i < Math.Max(a.Length, b.Length); i++) {
            if (i >= a.Length) {
                return -1;
            }

            if (i >= b.Length) {
                return 1;
            }

            var leftNumeric = int.TryParse(a[i], NumberStyles.None, CultureInfo.InvariantCulture, out var x);
            var rightNumeric = int.TryParse(b[i], NumberStyles.None, CultureInfo.InvariantCulture, out var y);

            // SemVer §11: numeric identifiers always sort below alphanumeric ones.
            var comparison = leftNumeric && rightNumeric
                ? x.CompareTo(y)
                : leftNumeric != rightNumeric
                ? leftNumeric ? -1 : 1
                : string.CompareOrdinal(a[i], b[i]);

            if (comparison != 0) {
                return comparison;
            }
        }

        return 0;
    }

    public static bool operator <(SemanticVersion? left, SemanticVersion? right) =>
        left is null ? right is not null : left.CompareTo(right) < 0;

    public static bool operator <=(SemanticVersion? left, SemanticVersion? right) =>
        left is null || left.CompareTo(right) <= 0;

    public static bool operator >(SemanticVersion? left, SemanticVersion? right) => right < left;

    public static bool operator >=(SemanticVersion? left, SemanticVersion? right) => right <= left;

    public override string ToString() =>
        IsPreRelease
            ? string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}-{PreRelease}")
            : string.Create(CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    [GeneratedRegex(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<pre>[0-9A-Za-z.-]+))?(?:\+[0-9A-Za-z.-]+)?$"
    )]
    private static partial Regex Pattern();
}
