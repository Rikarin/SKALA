using Rikarin.Skala.Core.Configuration;
using System.Text;

namespace Rikarin.Skala.Server;

/// <summary>
///     The behaviour behind <c>skala hooks</c>.
/// </summary>
/// <remarks>
///     ⚠ Here rather than in the CLI for the same reason <see cref="Formatting.CSharp.FormatCommand" />
///     is: nothing may reference <c>Rikarin.Skala.Cli</c> (docs/plan/02 § "The project graph"), and the
///     CLI is argument parsing and rendering only.
/// </remarks>
public static class HooksCommands {
    public static CommandResult InstallHooks(string repositoryRoot, bool apply) {
        var result = GitHooks.Install(repositoryRoot, apply);
        var output = new StringBuilder();
        output.Append(result.Path).Append(": ").AppendLine(result.Outcome);
        if (!apply && !result.Written && result.Outcome.StartsWith("would ", StringComparison.Ordinal)) {
            output.AppendLine("Pass --apply to write it.");
        }

        return new(0, output.ToString());
    }
}
