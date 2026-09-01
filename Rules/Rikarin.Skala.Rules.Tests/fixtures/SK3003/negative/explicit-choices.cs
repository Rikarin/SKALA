// analyzer-option: resharper_configure_await_analysis_mode = library
using System.Threading.Tasks; class C { async Task M(Task task, bool capture) { await task.ConfigureAwait(false); await task.ConfigureAwait(true); await task.ConfigureAwait(capture); } }
