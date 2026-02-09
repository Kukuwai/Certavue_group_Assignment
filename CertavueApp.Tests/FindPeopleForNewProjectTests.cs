using Xunit;
using System.Collections.Generic;

public class FindPeopleForNewProjectTests
{
  [Fact]
  public void FindPeopleForNewProject_SetsPropertiesCorrectly()
  {
    // ARRANGE
    var people = new List<Person>();
    var projects = new List<Project>();
    var state = new ScheduleState(people, projects);
    var finder = new AvailabilityFinder(state);

    // ACT
    var result = finder.FindPeopleForNewProject(startWeek: 10, duration: 5, peopleNeeded: 2);

    // ASSERT
    Assert.Equal(10, result.StartWeek);
    Assert.Equal(5, result.Duration);
    Assert.Equal(14, result.EndWeek);  // 10 + 5 - 1
    Assert.Equal(2, result.PeopleNeeded);
  }

  [Fact]
  public void FindPeopleForNewProject_AllPeopleFree_ReturnsAllPeople()
  {
    // ARRANGE
    var alice = new Person("Alice", 40, "Developer");
    var bob = new Person("Bob", 40, "QA");

    var people = new List<Person> { alice, bob };
    var projects = new List<Project>();
    var state = new ScheduleState(people, projects);
    var finder = new AvailabilityFinder(state);

    // ACT
    var result = finder.FindPeopleForNewProject(startWeek: 1, duration: 5, peopleNeeded: 2);

    // ASSERT
    Assert.Equal(2, result.AvailablePeople.Count);
    Assert.True(result.CanBeFulfilled);
  }

  [Fact]
  public void FindPeopleForNewProject_AllPeopleBusy_ReturnsNoPeople()
  {
    // ARRANGE
    var alice = new Person("Alice", 40, "Developer");
    var projectA = new Project("Project_A", 1, 10, 80);

    alice.projects[projectA] = new List<int> { 1, 2, 3, 4, 5 };
    projectA.people.Add(alice);

    var people = new List<Person> { alice };
    var projects = new List<Project> { projectA };
    var state = new ScheduleState(people, projects);



    var finder = new AvailabilityFinder(state);




    // ACT
    var result = finder.FindPeopleForNewProject(startWeek: 1, duration: 5, peopleNeeded: 1);

    // ASSERT
    Assert.Empty(result.AvailablePeople);
    Assert.False(result.CanBeFulfilled);
  }

  [Fact]
  public void FindPeopleForNewProject_NotEnoughPeople_CanBeFulfilledIsFalse()
  {
    // ARRANGE
    var alice = new Person("Alice", 40, "Developer");

    var people = new List<Person> { alice };
    var projects = new List<Project>();
    var state = new ScheduleState(people, projects);
    var finder = new AvailabilityFinder(state);


    // ACT
    var result = finder.FindPeopleForNewProject(startWeek: 10, duration: 5, peopleNeeded: 3);

    // ASSERT
    Assert.Single(result.AvailablePeople);
    Assert.False(result.CanBeFulfilled);  // Need 3 but only 1 available
  }

  [Fact]
  public void FindPeopleForNewProject_PersonBusyInOneWeek_NotIncluded()
  {
    // ARRANGE
    var alice = new Person("Alice", 40, "Developer");
    var bob = new Person("Bob", 40, "QA");
    var projectA = new Project("Project_A", 1, 10, 80);


    // Alice busy in week 12 (in middle of new project weeks 10-15)
    alice.projects[projectA] = new List<int> { 12 };
    projectA.people.Add(alice);

    var people = new List<Person> { alice, bob };
    var projects = new List<Project> { projectA };
    var state = new ScheduleState(people, projects);
    var finder = new AvailabilityFinder(state);









    // ACT
    var result = finder.FindPeopleForNewProject(startWeek: 10, duration: 6, peopleNeeded: 1);



    // ASSERT;
    Assert.Single(result.AvailablePeople);
    Assert.Contains(bob, result.AvailablePeople);  // Only Bob is free
    Assert.DoesNotContain(alice, result.AvailablePeople);  // Alice busy in week 12


  }
}