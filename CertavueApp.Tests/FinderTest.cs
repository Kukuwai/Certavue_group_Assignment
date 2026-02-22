using Xunit;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

public class FinderTest
{
    [Fact]
    public void GetPeopleForProject()
    {
        var a = new Project("a", startDate: 1, endDate: 10, hoursNeeded: 80);
        var b = new Project("b", 5, 15, 60);

        var alice = new Person("Alice", capacity: 40, role: "Engineer");
        alice.projects[a] = new List<int> { 1, 2, 3, 4 };
        alice.projects[b] = new List<int> { 6 };
        var bob = new Person("Bob", 40, "QA");
        bob.projects[a] = new List<int> { 2, 3, 5 };

        var carol = new Person("Carol", 40, "PM");
        carol.projects[a] = new List<int> { 4, 5, 6 };

        a.people.Add(alice);
        a.people.Add(bob);
        a.people.Add(carol);
        b.people.Add(alice);

        //call method here

        Assert.Equal(3, result.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result["Alice"].OrderBy(x => x));
        Assert.Equal(new[] { 2, 3, 5 }, result["Bob"].OrderBy(x => x));
        Assert.Equal(new[] { 4, 5, 6 }, result["Carol"].OrderBy(x => x));
    }

    [Fact]
    public void GetProjectForPerson()
    {
        var a = new Project("a", startDate: 1, endDate: 10, hoursNeeded: 80);
        var b = new Project("b", 5, 15, 60);

        var alice = new Person("Alice", capacity: 40, role: "Engineer");
        alice.projects[a] = new List<int> { 1, 2, 3, 4 };
        alice.projects[b] = new List<int> { 6 };
        var bob = new Person("Bob", 40, "QA");
        bob.projects[a] = new List<int> { 2, 3, 5 };

        var carol = new Person("Carol", 40, "PM");
        carol.projects[a] = new List<int> { 4, 5, 6 };

        a.people.Add(alice);
        a.people.Add(bob);
        a.people.Add(carol);
        b.people.Add(alice);

        //call method here

        Assert.Equal(2, result.Count);
        Assert.Equal(new[] { 1, 2, 3, 4 }, result[a].OrderBy(w => w));
        Assert.Equal(new[] { 6 }, result[b].OrderBy(w => w));
    }
}
