using Xunit;
using System.Collections.Generic;


public class FindWorkForNewPersonTests
{
  [Fact]
  public void FindWorkForNewPerson_ReturnsCorrectPersonName()
  {
    // ARRANGE
    var people = new List<Person>();
    var projects = new List<Project>();
    var state = new ScheduleState(people, projects);
    var finder = new AvailabilityFinder(state);


    // ACT
    var result = finder.FindWorkForNewPerson("Sam", 52);


    // ASSERT
    Assert.Equal("Sam", result.PersonName);
    Assert.Equal(52, result.AvailableWeeks);
  }


  [Fact]
  public void FindWorkForNewPerson_NoOverloadedPeople_ReturnsZeroOpportunities()
  {
    // ARRANGE
    var alice = new Person("Alice", 40, "Developer");
    var projectA = new Project("Project_A", 1, 10, 80);
    


    // Alice works 1 project only (not overloaded)
    alice.projects[projectA] = new List<int> { 1, 2, 3 };
    projectA.people.Add(alice);


    var people = new List<Person> { alice };
    var projects = new List<Project> { projectA };
    var state = new ScheduleState(people, projects);
    var finder = new AvailabilityFinder(state);


    // ACT
    var result = finder.FindWorkForNewPerson("Sam");


    // ASSERT
    Assert.Equal(0, result.ProjectsNeedingHelp);
    Assert.Equal(0, result.TotalWeeksAvailable);
    Assert.Empty(result.WorkOpportunities);
  }


  [Fact]
  public void FindWorkForNewPerson_WithOverloadedPerson_FindsOpportunities()
  {
    // ARRANGE
    var alice = new Person("Alice", 40, "Developer");
    var projectA = new Project("Project_A", 1, 10, 80);
    var projectB = new Project("Project_B", 1, 10, 60);


    // Alice works on BOTH projects in same weeks (overloaded!)
    alice.projects[projectA] = new List<int> { 5, 6, 7 };
    alice.projects[projectB] = new List<int> { 5, 6, 7 };
    projectA.people.Add(alice);
    projectB.people.Add(alice);


    var people = new List<Person> { alice };
    var projects = new List<Project> { projectA, projectB };
    var state = new ScheduleState(people, projects);
    var finder = new AvailabilityFinder(state);


    // ACT
    var result = finder.FindWorkForNewPerson("Sam");
  // ASSERT
    Assert.True(result.ProjectsNeedingHelp > 0);
    Assert.True(result.TotalWeeksAvailable > 0);
    Assert.NotEmpty(result.WorkOpportunities);

  }

}

