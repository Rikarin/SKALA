// skala-oracle: resharper=2025.2.6 config=sha256:9bf4b7e7193c5da3 profile=SkalaFormatOnly generated=2026-09-04
using System.Collections.Generic;
using System.Linq;

// JoinClause and JoinIntoClause occurred once each, and LetClause four times. A query is a
// Continuation whose clauses each start a line, so `resharper_csharp_wrap_linq_expressions` and
// `resharper_csharp_place_linq_into_on_new_line` decide the whole shape — and a join is the widest
// clause the language has, with three sub-expressions and an `into` that may or may not follow it.
class QueryJoins {
    record Person(int Id, string Name, int DepartmentId);

    record Department(int Id, string Name);

    static IEnumerable<string> Inner(IEnumerable<Person> people, IEnumerable<Department> departments) =>
        from person in people
        join department in departments on person.DepartmentId equals department.Id
        select person.Name + " / " + department.Name;

    static IEnumerable<string> Grouped(IEnumerable<Person> people, IEnumerable<Department> departments) =>
        from department in departments
        join person in people on department.Id equals person.DepartmentId into members
        select department.Name + " (" + members.Count() + ")";

    static IEnumerable<string> LeftOuter(IEnumerable<Person> people, IEnumerable<Department> departments) =>
        from department in departments
        join person in people on department.Id equals person.DepartmentId into members
        from member in members.DefaultIfEmpty()
        select department.Name + " / " + (member?.Name ?? "(nobody)");

    static IEnumerable<string> Composite(IEnumerable<Person> people, IEnumerable<Department> departments) =>
        from person in people
        join department in departments on new { Key = person.DepartmentId, person.Name } equals new {
            Key = department.Id, department.Name
        }
        let label = person.Name + " in " + department.Name
        where label.Length > 4
        orderby department.Name, person.Name descending
        group label by department.Name
        into byDepartment
        select byDepartment.Key + ": " + byDepartment.Count();
}
