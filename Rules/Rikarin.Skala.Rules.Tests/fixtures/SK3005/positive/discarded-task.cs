using System.Threading.Tasks;

sealed class Worker {
    public Worker() {
        SaveAsync();
    }

    public void Start() {
        SaveAsync();
    }

    static Task SaveAsync() => Task.CompletedTask;
}
