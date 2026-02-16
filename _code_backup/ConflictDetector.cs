using System;
using System.Collections.Generic;
using System.Linq;
using static GreedyAlg;
using static ScheduleState;

public class ConflictDetector
{
  // // This is initial method to test the Detection and then report. This can be removed later but for now keep it. 
  public ConflictReport DetectConflictsSimple(Dictionary<(int personId, int week), int> grid)
  {
    var report = new ConflictReport();

    foreach (var ((personId, week), count) in grid)
    {
      if (count > 1)
      {
        report.Conflicts.Add(new Conflict
        {
          PersonId = personId,
          PersonName = $"Person_{personId:D2}", // Temporary placeholder to test.
          Week = week,
          ProjectCount = count,
          ProjectNames = new List<string> { "Unknown" } // Temporary for now.
        });
      }
    }

    return report;
  }

  // This method gives a list of Projects a person is working in a week. 
  private List<string> GetProjectsForPersonInWeek(ScheduleState state, int personId, int week)

  {
    var projects = new List<string>();
    var person = state.People.First(p => p.id == personId);

    foreach (var project in state.Projects)
    {
      if (!project.people.Contains(person)) continue;

      var shift = state.GetShift(project);
      var footprint = state.GetGrid(project, shift);

      if (footprint.Any(f => f.PersonId == personId && f.Week == week))
      {
        projects.Add(project.name);
      }
    }

    return projects;
  }


  // This method gives a conflict report for all persons when they are booked more than once in a week. 

  public ConflictReport AnalyzeSchedule(ScheduleState state)
  {
    var report = new ConflictReport();

    foreach (var kvp in state.PersonWeekGrid)
    {
      var personId = kvp.Key.PersonId;
      var week = kvp.Key.Week;
      var count = kvp.Value;

      if (count > 1)
      {
        var person = state.People.First(p => p.id == personId);
        var projectsThisWeek = GetProjectsForPersonInWeek(state, personId, week);

        report.Conflicts.Add(new Conflict
        {
          PersonId = personId,
          PersonName = person.name,
          Week = week,
          ProjectCount = count,
          ProjectNames = projectsThisWeek
        });
      }
    }

    return report;
  }
}
// This class stores Conflicts, prints them, calculate statistics, counts affected people and groups conflicts by weeks and persons. 

public class ConflictReport
{
  public List<Conflict> Conflicts { get; set; } = new List<Conflict>();

  public int TotalConflictWeeks { get; set; }
  public int PeopleAffected { get; set; }
  public double ConflictPercentage { get; set; }

  public Dictionary<string, int> ConflictsByPerson { get; set; } = new Dictionary<string, int>();
  public Dictionary<int, int> ConflictsByWeek { get; set; } = new Dictionary<int, int>();

  public void PrintReport()
  {
    Console.WriteLine($"\nTotal conflicts found: {Conflicts.Count}");
    foreach (var conflict in Conflicts)
    {
      Console.WriteLine($"{conflict.PersonName}, Week {conflict.Week}: {conflict.ProjectCount} projects");
    }
  }

  public void CalculateStatistics(ScheduleState state)
  {
    TotalConflictWeeks = Conflicts.Count;
    PeopleAffected = Conflicts.Select(c => c.PersonName).Distinct().Count();
    int totalWeeks = state.PersonWeekGrid.Count;
    ConflictPercentage = totalWeeks > 0 ? (double)TotalConflictWeeks / totalWeeks * 100 : 0;

    // group conflicts by Persons.
    ConflictsByPerson = Conflicts
    .GroupBy(c => c.PersonName)
    .ToDictionary(g => g.Key, g => g.Count());
    // group conflcits by Weeks.

    ConflictsByWeek = Conflicts
        .GroupBy(c => c.Week)
        .ToDictionary(g => g.Key, g => g.Count());
  }
}
public class Conflict
{
  public int PersonId { get; set; }
  public string PersonName { get; set; }
  public int Week { get; set; }
  public int ProjectCount { get; set; }
  public List<string> ProjectNames { get; set; }
}