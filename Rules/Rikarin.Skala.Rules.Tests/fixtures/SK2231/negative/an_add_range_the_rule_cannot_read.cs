using System.Collections;
using System.Data;

public sealed class Orders {
    public void Load(int id, object[] rest) {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id and status = @status";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddRange(rest);
        command.ExecuteNonQuery();
    }
}

sealed class Bag : ArrayList, IDataParameterCollection {
    public object this[string name] { get => null!; set { } }

    public bool Contains(string name) => false;

    public int IndexOf(string name) => -1;

    public void RemoveAt(string name) { }

    public int Add(string name, object value) => Add((object)name);

    public int AddWithValue(string name, object value) => Add((object)name);
}

sealed class Parameter {
    public Parameter(string name, object value) { }
}

sealed class Command : IDbCommand {
    public string CommandText { get; set; } = "";

    public int CommandTimeout { get; set; }

    public CommandType CommandType { get; set; }

    public IDbConnection Connection { get; set; } = null!;

    // Typed as the concrete collection, the way every real provider does it -- `SqlCommand`
    // exposes `SqlParameterCollection`, not `IDataParameterCollection`, and `AddWithValue` lives
    // only on the concrete one.
    public Bag Parameters { get; } = new Bag();

    IDataParameterCollection IDbCommand.Parameters => Parameters;

    public IDbTransaction Transaction { get; set; } = null!;

    public UpdateRowSource UpdatedRowSource { get; set; }

    public void Cancel() { }

    public IDbDataParameter CreateParameter() => null!;

    public void Dispose() { }

    public int ExecuteNonQuery() => 0;

    public IDataReader ExecuteReader() => null!;

    public IDataReader ExecuteReader(CommandBehavior behavior) => null!;

    public object ExecuteScalar() => null!;

    public void Prepare() { }
}
