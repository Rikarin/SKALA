// analyzer-option: resharper_configure_await_analysis_mode = library
using System.Threading.Tasks; class C { async ValueTask<int> M(ValueTask<int> task) => await task; }
