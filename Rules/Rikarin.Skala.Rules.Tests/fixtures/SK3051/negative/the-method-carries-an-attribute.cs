using System;
using System.Threading.Tasks;

public sealed class HttpGetAttribute : Attribute { }

public sealed class Controller {
    [HttpGet]
    public async Task<string> IndexAsync() {
        await Task.Delay(5);
        return "ok";
    }
}
