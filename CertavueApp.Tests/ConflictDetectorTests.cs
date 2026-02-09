using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

public class ConflictDetectorTests
{
  // Create a simple test schedule
  private (ScheduleState state, List<Person> people, List<Project> projects) CreateTestSchedule()
  {
    var people = new List<Person>
        {
            new Person("Person_01", "Developer"),
            new Person("Person_02", "Designer")
        };

    var projects = new List<Project>
        {
            new Project("Project_A", 10, 20),
            new Project("Project_B", 15, 25)
        };

    // Person_01 on Project_A, weeks 12-14
    people[0].projects[projects[0]] = new List<int> { 12, 13, 14 };
    projects[0].people.Add(people[0]);

    // Person_02 on both projects, weeks 16-18 (creates conflicts!)
    people[1].projects[projects[0]] = new List<int> { 16, 17, 18 };
    people[1].projects[projects[1]] = new List<int> { 16, 17, 18 };
    projects[0].people.Add(people[1]);
    projects[1].people.Add(people[1]);

    var state = new ScheduleState(people, projects);
    return (state, people, projects);
  }

  [Fact]
  public void Test1_AnalyzeSchedule_DetectsConflicts()
  {
    var (state, _, _) = CreateTestSchedule();
    var detector = new ConflictDetector();

    var report = detector.AnalyzeSchedule(state);

    Assert.NotNull(report);
    Assert.True(report.Conflicts.Count > 0, "Should detect conflicts");
    Console.WriteLine($"Test 1 Passed: Found {report.Conflicts.Count} conflicts");

  }

  [Fact]
  public void Test2_AnalyzeSchedule_CountsCorrectly()
  {
    var (state, _, _) = CreateTestSchedule();
    var detector = new ConflictDetector();

    var report = detector.AnalyzeSchedule(state);

    // Person_02 has conflicts in weeks 16, 17, 18
    Assert.Equal(3, report.Conflicts.Count);
    Console.WriteLine($"Test 2 Passed: Counted {report.Conflicts.Count} conflicts");
  }
  [Fact]
  public void Test3_AnalyzeSchedule_IdentifiesCorrectPerson()
  {
    var (state, people, _) = CreateTestSchedule();
    var detector = new ConflictDetector();

    var report = detector.AnalyzeSchedule(state);

    // All conflicts should be for Person_02
    Assert.All(report.Conflicts, c => Assert.Equal("Person_02", c.PersonName));
    Console.WriteLine($"Test 3 Passed: All conflicts are Person_02");
  }
  [Fact]
  public void Test4_CalculateStatistics_Works()
  {
    var (state, _, _) = CreateTestSchedule();
    var detector = new ConflictDetector();

    var report = detector.AnalyzeSchedule(state);
    report.CalculateStatistics(state);

    Assert.Equal(3, report.TotalConflictWeeks);
    Assert.Equal(1, report.PeopleAffected); // // Only Person_02 affected
    Console.WriteLine($"Test 4 Passed: Statistics calculated correctly");
  }
  [Fact]
  public void Test5_EmptySchedule_NoConflicts()
  {
    var emptyPeople = new List<Person>();
    var emptyProjects = new List<Project>();
    var state = new ScheduleState(emptyPeople, emptyProjects);
    var detector = new ConflictDetector();

    var report = detector.AnalyzeSchedule(state);

    Assert.Empty(report.Conflicts);
    Console.WriteLine($"Test 5 Passed: Empty schedule has no conflicts");

  }
}







