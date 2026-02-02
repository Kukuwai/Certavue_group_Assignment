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
            new Person("Person_01"),
            new Person("Person_02")
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
    // ARRANGE
    var (state, _, _) = CreateTestSchedule();
    var detector = new ConflictDetector();

    // ACT
    var report = detector.AnalyzeSchedule(state);

    // ASSERT
    Assert.NotNull(report);
    Assert.True(report.Conflicts.Count > 0, "Should detect conflicts");
    Console.WriteLine($"Test 1 Passed: Found {report.Conflicts.Count} conflicts");
  }

  [Fact]
  public void Test2_AnalyzeSchedule_CountsCorrectly()
  {
    // ARRANGE
    var (state, _, _) = CreateTestSchedule();
    var detector = new ConflictDetector();

    // ACT
    var report = detector.AnalyzeSchedule(state);

    // ASSERT - Person_02 has conflicts in weeks 16, 17, 18
    Assert.Equal(3, report.Conflicts.Count);
    Console.WriteLine($"Test 2 Passed: Counted {report.Conflicts.Count} conflicts");
  }

}