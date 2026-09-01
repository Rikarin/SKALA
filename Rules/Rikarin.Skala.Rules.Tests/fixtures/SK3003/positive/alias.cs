// analyzer-option: configure_await_analysis_mode = library
using System.Threading.Tasks; class C { async Task<int> M(Task<int> task) => await task; }
