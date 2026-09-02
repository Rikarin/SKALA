#!/usr/bin/env python3
"""Write the SK2230-SK2233 fixture sets."""
import pathlib

ROOT = pathlib.Path(__file__).resolve().parent / "Rules" / "Rikarin.Skala.Rules.Tests" / "fixtures"

# The IDbCommand scaffolding SK2231's fixtures need. System.Data.Common ships only abstract
# types, so every fixture that wants a command has to bring one.
USINGS = "using System.Collections;\nusing System.Data;\n\n"

COMMAND = """
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
"""

FIXTURES = {
    # ---------------------------------------------------------------- SK2230
    ("SK2230", "positive", "where_runs_onto_the_table_name"): """
public sealed class Queries {
    public string ByActive() =>
        "select id, name from users"
        + "where active = 1";
}
""",
    ("SK2230", "positive", "order_by_runs_onto_the_last_column"): """
public sealed class Queries {
    public string Newest() =>
        "select id from events where kind = 3"
        + "order by created desc";
}
""",
    ("SK2230", "positive", "and_runs_onto_a_literal"): """
public sealed class Queries {
    public string Both() =>
        "select * from orders where paid = 1"
        + "and shipped = 0";
}
""",
    ("SK2230", "positive", "the_join_is_in_the_middle_of_three"): """
public sealed class Queries {
    public string Update() =>
        "update accounts "
        + "set balance = 0"
        + "where id = 7";
}
""",
    ("SK2230", "negative", "the_left_fragment_ends_with_a_space"): """
public sealed class Queries {
    public string ByActive() =>
        "select id, name from users "
        + "where active = 1";
}
""",
    ("SK2230", "negative", "the_right_fragment_opens_with_a_space"): """
public sealed class Queries {
    public string ByActive() =>
        "select id, name from users"
        + " where active = 1";
}
""",
    ("SK2230", "negative", "a_table_name_split_over_two_lines"): """
// ⚠ `Order` is a SQL keyword and this fusion is deliberate: the table is `OrderItems`. It is why
// the rule tests the word the *right* fragment opens with and never the one the left one ends on.
public sealed class Queries {
    public string Items() =>
        "select * from Order"
        + "Items where id = 1";
}
""",
    ("SK2230", "negative", "the_fusion_is_inside_a_sql_string"): """
// The apostrophes before the join are odd, so the join is inside a `'…'` literal. A space there
// would change the value the statement compares against rather than repair how it parses.
public sealed class Queries {
    public string Failed() =>
        "select * from logs where message = 'timed out at the end"
        + "ORDER'";
}
""",
    ("SK2230", "negative", "nothing_here_opens_a_statement"): """
public sealed class Report {
    public string Line() =>
        "the operation finished"
        + "where it started";
}
""",
    ("SK2230", "negative", "the_word_after_the_join_is_not_a_keyword"): """
public sealed class Queries {
    public string Settings() =>
        "select * from config where scope = 'off"
        + "setting'";
}
""",
    ("SK2230", "negative", "a_star_and_a_keyword_do_not_fuse"): """
// ⚠ `"select *" + "from t"` is `select *from t`, which every SQL tokenizer accepts: `*` cannot be
// part of the same token as `f`. The defect needs two *word* characters to meet.
public sealed class Queries {
    public string All() =>
        "select *"
        + "from t";
}
""",
    ("SK2230", "negative", "one_operand_is_not_a_literal"): """
public sealed class Queries {
    public string ByTable(string table) =>
        "select * from "
        + table
        + "where id = 1";
}
""",
    ("SK2230", "negative", "an_interpolated_fragment"): """
public sealed class Queries {
    public string ByTable(string table) =>
        $"select * from {table}"
        + "where id = 1";
}
""",
    ("SK2230", "negative", "a_raw_string_literal"): """
public sealed class Queries {
    public string ByActive() =>
        \"\"\"select id, name from users\"\"\"
        + \"\"\"where active = 1\"\"\";
}
""",
    ("SK2230", "negative", "a_single_literal_with_no_join"): """
public sealed class Queries {
    public string ByActive() => "select id, name from users where active = 1";
}
""",
    # ---------------------------------------------------------------- SK2232
    ("SK2232", "positive", "load_from_in_the_resolver"): """
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    readonly string directory;

    public PluginContext(string directory) => this.directory = directory;

    protected override Assembly Load(AssemblyName name) =>
        Assembly.LoadFrom(directory + "/" + name.Name + ".dll");
}
""",
    ("SK2232", "positive", "load_file_in_the_resolver"): """
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    readonly string directory;

    public PluginContext(string directory) => this.directory = directory;

    protected override Assembly Load(AssemblyName name) {
        var path = directory + "/" + name.Name + ".dll";
        return Assembly.LoadFile(path);
    }
}
""",
    ("SK2232", "negative", "assembly_load_shares_the_contract"): """
// ⚠ The exclusion the rule exists for. Returning `Assembly.Load` from the override says "this
// dependency is shared -- take it from the default context", which is how a plugin and its host
// agree on a contract assembly. Contradicting that would be the false positive.
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    protected override Assembly Load(AssemblyName name) => Assembly.Load(name);
}
""",
    ("SK2232", "negative", "already_loading_into_this_context"): """
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    readonly string directory;

    public PluginContext(string directory) => this.directory = directory;

    protected override Assembly Load(AssemblyName name) =>
        LoadFromAssemblyPath(directory + "/" + name.Name + ".dll");
}
""",
    ("SK2232", "negative", "load_from_outside_any_load_context"): """
using System.Reflection;

public sealed class Plugins {
    public Assembly Open(string path) => Assembly.LoadFrom(path);
}
""",
    ("SK2232", "negative", "load_from_in_another_member_of_the_context"): """
// The context declares this helper and never calls it from `Load`. Whether the default context is
// where that assembly belongs is not stated anywhere, so the rule does not decide it.
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    public Assembly OpenTool(string path) => Assembly.LoadFrom(path);

    protected override Assembly Load(AssemblyName name) => LoadFromAssemblyName(name);
}
""",
    ("SK2232", "negative", "inside_a_lambda_in_the_override"): """
using System;
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    protected override Assembly Load(AssemblyName name) {
        Func<string, Assembly> open = static path => Assembly.LoadFrom(path);
        return open(name.Name + ".dll");
    }
}
""",
    ("SK2232", "negative", "inside_a_local_function_in_the_override"): """
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    protected override Assembly Load(AssemblyName name) {
        return Open(name.Name + ".dll");

        static Assembly Open(string path) => Assembly.LoadFrom(path);
    }
}
""",
    ("SK2232", "negative", "an_override_of_something_else"): """
using System;
using System.Reflection;
using System.Runtime.Loader;

public sealed class PluginContext : AssemblyLoadContext {
    protected override IntPtr LoadUnmanagedDll(string name) {
        var probe = Assembly.LoadFrom(name + ".dll");
        return probe is null ? IntPtr.Zero : IntPtr.Zero;
    }
}
""",
    # ---------------------------------------------------------------- SK2233
    ("SK2233", "positive", "enum_get_values_on_a_class"): """
using System;

public sealed class Widget { }

public sealed class Registry {
    public Array All() => Enum.GetValues(typeof(Widget));
}
""",
    ("SK2233", "positive", "enum_parse_on_a_struct"): """
using System;

public struct Point { }

public sealed class Registry {
    public object Read(string text) => Enum.Parse(typeof(Point), text);
}
""",
    ("SK2233", "positive", "get_custom_attribute_on_a_non_attribute"): """
using System;
using System.Reflection;

public sealed class Marker { }

public sealed class Registry {
    public Attribute? Read(MemberInfo member) => Attribute.GetCustomAttribute(member, typeof(Marker));
}
""",
    ("SK2233", "positive", "create_instance_of_an_interface"): """
using System;

public interface IWidget { }

public sealed class Factory {
    public object? Make() => Activator.CreateInstance(typeof(IWidget));
}
""",
    ("SK2233", "positive", "create_instance_of_an_abstract_class"): """
using System;

public abstract class Shape { }

public sealed class Factory {
    public object? Make() => Activator.CreateInstance(typeof(Shape));
}
""",
    ("SK2233", "negative", "enum_get_values_on_an_enum"): """
using System;

public enum Kind { First, Second }

public sealed class Registry {
    public Array All() => Enum.GetValues(typeof(Kind));
}
""",
    ("SK2233", "negative", "enum_is_defined_on_an_enum"): """
using System;

public enum Kind { First, Second }

public sealed class Registry {
    public bool Known(int value) => Enum.IsDefined(typeof(Kind), value);
}
""",
    ("SK2233", "negative", "get_custom_attribute_on_an_attribute"): """
using System;
using System.Reflection;

public sealed class MarkerAttribute : Attribute { }

public sealed class Registry {
    public Attribute? Read(MemberInfo member) => Attribute.GetCustomAttribute(member, typeof(MarkerAttribute));
}
""",
    ("SK2233", "negative", "typeof_attribute_itself_means_any"): """
// `GetCustomAttribute(member, typeof(Attribute))` means "any attribute" and is exactly right. The
// test is derives-from-*or-equals*, which is the whole difference.
using System;
using System.Reflection;

public sealed class Registry {
    public Attribute? Any(MemberInfo member) => Attribute.GetCustomAttribute(member, typeof(Attribute));
}
""",
    ("SK2233", "negative", "create_instance_of_a_concrete_class"): """
using System;

public sealed class Widget { }

public sealed class Factory {
    public object? Make() => Activator.CreateInstance(typeof(Widget));
}
""",
    ("SK2233", "negative", "a_type_held_in_a_variable"): """
// The `Type` arrived from a caller. What it holds is not a fact in this file, which is the same
// reason SK5001 refuses to treat a parameter as a source.
using System;

public sealed class Registry {
    public Array All(Type kind) => Enum.GetValues(kind);
}
""",
    ("SK2233", "negative", "a_type_parameter_operand"): """
using System;

public sealed class Registry {
    public Array All<T>() where T : struct, Enum => Enum.GetValues(typeof(T));
}
""",
    ("SK2233", "negative", "create_delegate_on_a_delegate"): """
using System;
using System.Reflection;

public sealed class Registry {
    public Delegate Bind(MethodInfo method) => Delegate.CreateDelegate(typeof(Action), method);
}
""",
    ("SK2233", "negative", "an_api_the_table_does_not_name"): """
using System;

public sealed class Widget { }

public sealed class Registry {
    public string? Describe() => typeof(Widget).FullName;

    public bool Same(object value) => value.GetType() == typeof(Widget);
}
""",
    ("SK2233", "negative", "an_open_generic_is_not_reported"): """
// `Activator.CreateInstance(typeof(List<>))` throws too, and is the one shape where an author may
// be closing the type from it somewhere this rule cannot see.
using System;
using System.Collections.Generic;

public sealed class Factory {
    public Type Open() => typeof(List<>);
}
""",
}

# SK2231's fixtures all need the command scaffolding, so they are built separately.
SK2231 = {
    ("positive", "one_of_two_markers_is_bound"): """
public sealed class Orders {
    public void Load(int id, int status) {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id and status = @status";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("positive", "the_parameter_object_names_only_one"): """
public sealed class Orders {
    public void Load(int id) {
        var command = new Command();
        command.CommandText = "update orders set status = @status where id = @id";
        command.Parameters.Add(new Parameter("@id", id));
        command.ExecuteNonQuery();
    }
}
""",
    ("positive", "the_added_name_has_no_sigil"): """
public sealed class Orders {
    public void Load(int id) {
        var command = new Command();
        command.CommandText = "delete from orders where id = @id and tenant = @tenant";
        command.Parameters.Add("id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "every_marker_is_bound"): """
public sealed class Orders {
    public void Load(int id, int status) {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id and status = @status";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@status", status);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "an_extra_parameter_is_not_a_finding"): """
// Most providers ignore a parameter the text never names, so the other direction is a much weaker
// finding and is not reported at all.
public sealed class Orders {
    public void Load(int id, int status) {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@status", status);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "no_parameter_is_added_at_all"): """
// Zero is the shape where the binding most plausibly happens somewhere this rule cannot see.
public sealed class Orders {
    public void Load() {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id";
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "the_command_escapes_the_method"): """
public sealed class Orders {
    public void Load(int id) {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id and status = @status";
        command.Parameters.AddWithValue("@id", id);
        Bind(command);
        command.ExecuteNonQuery();
    }

    static void Bind(Command command) => command.Parameters.AddWithValue("@status", 1);
}
""",
    ("negative", "a_computed_parameter_name"): """
// One name the rule cannot read makes every remaining marker unknowable, so the whole method is
// abandoned rather than half-understood.
public sealed class Orders {
    public void Load(int id, string key) {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id and status = @status";
        command.Parameters.AddWithValue("@" + key, id);
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "the_text_is_not_constant"): """
public sealed class Orders {
    public void Load(string clause, int id) {
        var command = new Command();
        command.CommandText = "select * from orders where " + clause + " and id = @id and x = @x";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "a_stored_procedure_name"): """
public sealed class Orders {
    public void Load(int id) {
        var command = new Command();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "load_orders @id @status";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "the_text_is_assigned_twice"): """
public sealed class Orders {
    public void Load(int id, bool wide) {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id and status = @status";
        if (wide) {
            command.CommandText = "select * from orders where id = @id";
        }

        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "a_tsql_global_is_not_a_parameter"): """
public sealed class Orders {
    public void Load(int id) {
        var command = new Command();
        command.CommandText = "insert into orders (id) values (@id); select @@identity";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "an_address_inside_a_sql_string"): """
// ⚠ This one proves the *preceded by a word character* guard and not the apostrophe guard -- `@`
// after `root` is skipped before the quote counting is ever consulted. Sabotaging the apostrophe
// skip left this fixture green, which is what `a_marker_inside_a_sql_string` exists for.
public sealed class Orders {
    public void Load(int id) {
        var command = new Command();
        command.CommandText = "select * from users where id = @id and mail = 'root@localhost'";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "a_marker_inside_a_sql_string"): """
// A marker shape inside a `'…'` literal, with a space before it so the word-character guard cannot
// reach it. Only the apostrophe counting declines this one.
public sealed class Orders {
    public void Load(int id) {
        var command = new Command();
        command.CommandText = "select * from tickets where id = @id and note = 'ask @support first'";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "a_marker_inside_a_comment"): """
public sealed class Orders {
    public void Load(int id) {
        var command = new Command();
        command.CommandText = "select * from orders /* was @status */ where id = @id -- and @tenant";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "the_command_is_a_field"): """
// A field is visible to every other method on the type, so what it has been given is not a fact
// this method holds.
public sealed class Orders {
    readonly Command command = new();

    public void Load(int id) {
        command.CommandText = "select * from orders where id = @id and status = @status";
        command.Parameters.AddWithValue("@id", id);
        command.ExecuteNonQuery();
    }
}
""",
    ("negative", "an_add_range_the_rule_cannot_read"): """
public sealed class Orders {
    public void Load(int id, object[] rest) {
        var command = new Command();
        command.CommandText = "select * from orders where id = @id and status = @status";
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddRange(rest);
        command.ExecuteNonQuery();
    }
}
""",
}


def write(path: pathlib.Path, body: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(body.lstrip("\n"), encoding="utf-8")


def main() -> None:
    for (rule, kind, name), body in FIXTURES.items():
        write(ROOT / rule / kind / (name + ".cs"), body)

    for (kind, name), body in SK2231.items():
        # ⚠ The usings go above everything. A fixture whose scaffolding carries its own `using`
        # block after the class under test is CS1529, and CS1529 is an *error*, which the harness
        # rejects before any rule runs — so the whole set would prove nothing rather than fail.
        text = body.lstrip("\n")
        lead = ""
        while text.startswith("//"):
            line, _, text = text.partition("\n")
            lead += line + "\n"

        write(ROOT / "SK2231" / kind / (name + ".cs"), lead + USINGS + text + COMMAND)

    print("wrote", len(FIXTURES) + len(SK2231), "fixtures")


if __name__ == "__main__":
    main()
