using System.Text;
using Rikarin.Skala.Core.Configuration;

namespace Rikarin.Skala.Server;

/// <summary>
/// The behaviour behind <c>skala daemon</c> and <c>skala hooks</c>.
/// </summary>
/// <remarks>
/// ⚠ Here rather than in the CLI for the same reason <see cref="Formatting.CSharp.FormatCommand"/>
/// is: nothing may reference <c>Rikarin.Skala.Cli</c> (docs/plan/02 § "The project graph"), and the
/// CLI is argument parsing and rendering only.
/// </remarks>
public static class DaemonCommands {
    public static CommandResult Status(string repositoryRoot) {
        var response = DaemonClient.Send(repositoryRoot, new DaemonRequest { Command = "status" });
        if (response is null) {
            return new CommandResult(0, $"no daemon on {DaemonProtocol.SocketPath(repositoryRoot)}\n");
        }

        return response.Ok
            ? new CommandResult(0, response.Status + "\n")
            : new CommandResult(1, response.Error + "\n");
    }

    public static CommandResult Stop(string repositoryRoot) {
        var response = DaemonClient.Send(repositoryRoot, new DaemonRequest { Command = "stop" });
        return response is null
            ? new CommandResult(0, "no daemon to stop\n")
            : new CommandResult(0, "stopped\n");
    }

    public static async Task<int> RunAsync(string repositoryRoot, CancellationToken cancellation) {
        await using var daemon = new Daemon(repositoryRoot);
        try {
            daemon.Listen();
        } catch (IOException exception) {
            await Console.Error.WriteLineAsync($"skala daemon: {exception.Message}").ConfigureAwait(false);
            return 2;
        }

        // ⚠ Guarded, because a lazily started daemon outlives the process that started it and
        // inherits that process's pipes: by the time it says anything else, the other end may be
        // gone. A banner is not worth a dead daemon.
        try {
            Console.WriteLine($"skala daemon {DaemonProtocol.Version} on {daemon.SocketPath}");
        } catch (IOException) { }

        await daemon.RunAsync(cancellation).ConfigureAwait(false);
        return 0;
    }

    public static CommandResult InstallHooks(string repositoryRoot, bool apply) {
        var result = GitHooks.Install(repositoryRoot, apply);
        var output = new StringBuilder();
        output.Append(result.Path).Append(": ").AppendLine(result.Outcome);
        if (!apply && !result.Written && result.Outcome.StartsWith("would ", StringComparison.Ordinal)) {
            output.AppendLine("Pass --apply to write it.");
        }

        return new CommandResult(0, output.ToString());
    }
}
