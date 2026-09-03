// analyzer-option: skala_configure_await_analysis_mode = disabled
// analyzer-option: skala_configure_await_analysis_mode = library
using System.Threading.Tasks; class C { async Task M(Task task) { await task; } }
