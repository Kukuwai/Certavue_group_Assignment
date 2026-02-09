using Xunit;
using System.Collections.Generic;

public class PersonTests
{
  [Fact]
  public void Person_Constructor_SetsPropertiesCorrectly()
  {
    // ACT
    var person = new Person("Alice", 40, "Developer");

    // ASSERT
    Assert.Equal("Alice", person.name);
    Assert.Equal(40, person.capacity);
    Assert.Equal("Developer", person.role);
    Assert.True(person.id > 0);
  }

  [Fact]
  public void Person_TwoParameterConstructor_Works()
  {
    // ACT
    var person = new Person("Bob", "QA");

    // ASSERT
    Assert.Equal("Bob", person.name);
    Assert.Equal("QA", person.role);
    Assert.Equal(0, person.capacity);  // Not set in 2-param constructor
  }

  [Fact]
  public void Person_CanAddProjects()
  {
    // ARRANGE
    var person = new Person("Carol", 40, "Manager");
    var project = new Project("Project_A", 1, 10, 80);

    // ACT
    person.projects[project] = new List<int> { 1, 2, 3 };

    // ASSERT
    Assert.Single(person.projects);
    Assert.Equal(3, person.projects[project].Count);
  }

  [Fact]
  public void Person_Id_AutoIncrements()
  {
    // ACT
    var person1 = new Person("Dave", 40, "Tester");
    var person2 = new Person("Eve", 40, "Designer");

    // ASSERT
    Assert.True(person2.id > person1.id);
  }
}