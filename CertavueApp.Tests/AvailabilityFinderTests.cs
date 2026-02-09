using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class AvailabilityFinderTests
{
  // Create a test schedule with known availability
  private (ScheduleState state, List<Person> people, List<Project> projects) CreateTestSchedule()
  {
    var people = new List<Person>
        {
            new Person("Person_01", "Developer"),   // Will be free
            new Person("Person_02", "Designer"),  // Will have 1 project
            new Person("Person_03", "Tester")   // Will have 2 projects (overloaded)
        };

    var projects = new List<Project>
        {
            new Project("Project_A", 10, 30),
            new Project("Project_B", 10, 30)
        };

    // Person_02 on Project_A, weeks 15-17 (1 project = normal)
    people[1].projects[projects[0]] = new List<int> { 15, 16, 17 };
    projects[0].people.Add(people[1]);




    // Person_03 on both projects, weeks 20-22 (2 projects = overloaded!)
    people[2].projects[projects[0]] = new List<int> { 20, 21, 22 };
    people[2].projects[projects[1]] = new List<int> { 20, 21, 22 };
    projects[0].people.Add(people[2]);
    projects[1].people.Add(people[2]);

    var state = new ScheduleState(people, projects);
    return (state, people, projects);
  }

  [Fact]
  public void Test1_GetPersonWorkload_ReturnsZero_WhenFree()
  {

    var (state, people, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    // Person_01 is completely free
    int workload = finder.GetPersonWorkload("Person_01", 15);

    Assert.Equal(0, workload);

    Console.WriteLine("Test1: GetPersonWorkload returns zero when person is free - PASSED");


  }

  [Fact]
  public void Test2_GetPersonWorkload_ReturnsCorrectCount_WhenBusy()
  {
    var (state, people, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    // Person_02 has 1 project in week 15
    int workload1 = finder.GetPersonWorkload("Person_02", 15);

    // Person_03 has 2 projects in week 20
    int workload2 = finder.GetPersonWorkload("Person_03", 20);

    Assert.Equal(1, workload1);
    Assert.Equal(2, workload2);
    Console.WriteLine("Test2: GetPersonWorkload returns correct count when busy - PASSED");
  }

  [Fact]
  public void Test3_GetPersonWorkload_ReturnsNegativeOne_WhenPersonNotFound()
  {
    var (state, _, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    int workload = finder.GetPersonWorkload("NonExistent", 15);

    Assert.Equal(-1, workload);
    Console.WriteLine("Test3: GetPersonWorkload returns -1 when person not found - PASSED");
  }

  [Fact]
  public void Test4_GetAvailablePeopleInWeek_ReturnsFreePeople()
  {
    var (state, people, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    // Week 15: Person_01 is free, Person_02 is busy, Person_03 is free
    var availablePeople = finder.GetAvailablePeopleInWeek(15);

    Assert.Equal(2, availablePeople.Count);  // Person_01 and Person_03
    Assert.Contains(availablePeople, p => p.name == "Person_01");
    Assert.Contains(availablePeople, p => p.name == "Person_03");
    Console.WriteLine("Test4: GetAvailablePeopleInWeek returns free people - PASSED");
  }

  [Fact]
  public void Test5_GetAvailableWeeksForPerson_ReturnsCorrectWeeks()
  {
    var (state, people, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    // Person_02 busy weeks 15-17, free rest of year
    var freeWeeks = finder.GetAvailableWeeksForPerson("Person_02");

    Assert.Equal(49, freeWeeks.Count);  // 52 - 3 = 49 free weeks
    Assert.DoesNotContain(15, freeWeeks);
    Assert.DoesNotContain(16, freeWeeks);
    Assert.DoesNotContain(17, freeWeeks);
    Assert.Contains(1, freeWeeks);
    Assert.Contains(50, freeWeeks);
    Console.WriteLine("Test5: GetAvailableWeeksForPerson returns correct weeks - PASSED");
  }

  [Fact]
  public void Test6_FindLeastBusyWeeks_ReturnsCorrectWeeks()
  {
    var (state, people, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    var leastBusy = finder.FindLeastBusyWeeks(5);

    Assert.Equal(5, leastBusy.Count);
    // Weeks 15-17 and 20-22 are busy, so these shouldn't be in top 5 least busy
    Assert.All(leastBusy, week =>
        Assert.True(week < 15 || (week > 17 && week < 20) || week > 22)
    );
  }

  [Fact]
  public void Test7_CountOverloadedWeeks_ReturnsZero_WhenNotOverloaded()
  {
    var (state, people, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    // Person_01 has no projects
    int overloadedCount1 = finder.CountOverloadedWeeks("Person_01");

    // Person_02 has only 1 project at a time
    int overloadedCount2 = finder.CountOverloadedWeeks("Person_02");

    Assert.Equal(0, overloadedCount1);
    Assert.Equal(0, overloadedCount2);
  }

  [Fact]
  public void Test8_CountOverloadedWeeks_ReturnsCorrectCount_WhenOverloaded()
  {
    var (state, people, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    // Person_03 has 2 projects in weeks 20, 21, 22
    int overloadedCount = finder.CountOverloadedWeeks("Person_03");

    Assert.Equal(3, overloadedCount);  // 3 weeks with 2+ projects
  }

  [Fact]
  public void Test9_GetAvailableWeeksForPerson_ReturnsEmpty_WhenPersonNotFound()
  {
    var (state, _, _) = CreateTestSchedule();
    var finder = new AvailabilityFinder(state);

    var weeks = finder.GetAvailableWeeksForPerson("NonExistent");

    Assert.Empty(weeks);
  }
}