using System.Threading.Tasks;

class C {
    public static async Task RunAsync() {
        await Task.Yield();
        return;
    }
}
