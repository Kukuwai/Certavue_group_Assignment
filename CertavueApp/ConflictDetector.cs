using System;
using System.Collections.Generic;
using System.Linq;
using static GreedyAlg;
using static ScheduleState;

/// <summary>
/// Detects scheduling problems by checking if people are assigned too many hours in any given week.
/// Flags two levels of concern: (1) Overloaded - when total hours exceed a person's weekly capacity, and 
/// (2) High Utilization - when people are working at 90-100% capacity with no buffer for urgent work.
/// Provides detailed reports showing which people, weeks, and projects are affected. It helps managers 
/// identify bottlenecks, prevent burnout, and make better staffing decisions.
/// </summary>

public class ConflictDetector
{
  /// <summary>
  /// Returns a list of project names that a specific person is allocated to in a given week,
  /// based on their hours assignments in the schedule state.
  /// </summary>
  private List<string> GetProjectsForPersonInWeek(ScheduleState state, int personId, int week)
  {
    var projects = new List<string>();
    var person = state.People.FirstOrDefault(p => p.id == personId);

    if (person == null) return projects;

    foreach (var projectEntry in person.projects)
    {
      var project = projectEntry.Key;
      var weekHours = projectEntry.Value;

      // Check if person has hours allocated in this week for this project
      if (weekHours.ContainsKey(week) && weekHours[week] > 0)
      {
        projects.Add(project.name);
      }
    }

    return projects;
  }

  /// <summary>
  /// Analyzes the entire schedule to detect conflicts (hours > capacity) and high utilization warnings (90-100% capacity),
  /// returning a comprehensive report for capacity planning and risk management.
  /// </summary>
  public ConflictReport AnalyzeSchedule(ScheduleState state)
  {
    var report = new ConflictReport();
    const int DEFAULT_CAPACITY = 40;

    foreach (var kvp in state.PersonWeekGrid)
    {
      var personId = kvp.Key.PersonId;
      var week = kvp.Key.Week;
      var totalHours = kvp.Value;

      var person = state.People.FirstOrDefault(p => p.id == personId);
      if (person == null) continue;

      int capacity = person.capacity > 0 ? person.capacity : DEFAULT_CAPACITY;

      // Flag OVERLOADED (exceeds capacity)
      if (totalHours > capacity)
      {
        var projectsThisWeek = GetProjectsForPersonInWeek(state, personId, week);
        report.Conflicts.Add(new Conflict
        {
          PersonId = personId,
          PersonName = person.name,
          Week = week,
          TotalHours = totalHours,
          Capacity = capacity,
          ProjectCount = projectsThisWeek.Count,
          ProjectNames = projectsThisWeek
        });
      }
      // Flag HIGH UTILIZATION (90-100% capacity)
      else if (totalHours >= capacity * 0.9)
      {
        var projectsThisWeek = GetProjectsForPersonInWeek(state, personId, week);
        report.HighUtilization.Add(new Conflict
        {
          PersonId = personId,
          PersonName = person.name,
          Week = week,
          TotalHours = totalHours,
          Capacity = capacity,
          ProjectCount = projectsThisWeek.Count,
          ProjectNames = projectsThisWeek
        });
      }
    }

    report.CalculateStatistics(state);
    return report;
  }
}
// This class stores Conflicts, prints them, calculate statistics, counts affected people and groups conflicts by weeks and persons. 

/// <summary>
/// Contains a comprehensive analysis of scheduling conflicts (overloaded) and high utilization warnings (near capacity),
/// including statistics grouped by person and week to support manager decision-making.
/// </summary>
public class ConflictReport
{
  public List<Conflict> Conflicts { get; set; } = new List<Conflict>();
  public List<Conflict> HighUtilization { get; set; } = new List<Conflict>();

  public int TotalConflictWeeks { get; set; }
  public int PeopleAffected { get; set; }
  public double ConflictPercentage { get; set; }
  public int TotalOverloadHours { get; set; }  // NEW: Total hours over capacity

  public Dictionary<string, int> ConflictsByPerson { get; set; } = new Dictionary<string, int>();
  public Dictionary<int, int> ConflictsByWeek { get; set; } = new Dictionary<int, int>();

  /// <summary>
  /// Prints a detailed summary of conflicts, high utilization warnings, and burnout risks to the console.
  /// </summary>
  public void PrintReport()
  {
    Console.WriteLine($"\n************** CONFLICT REPORT ***************");
    Console.WriteLine($"Total conflicts found: {Conflicts.Count} person-weeks (URGENT)");
    Console.WriteLine($"HIGH UTILIZATION: {HighUtilization.Count} person-weeks (90-100% capacity)");
    Console.WriteLine($"People affected: {PeopleAffected}");
    Console.WriteLine($"Total overload hours: {TotalOverloadHours}h");
    Console.WriteLine($"Conflict percentage: {ConflictPercentage:F2}%\n");

    // Show who's consistently maxed out
    var consistentlyMaxed = HighUtilization
        .GroupBy(c => c.PersonName)
        .Where(g => g.Count() >= 3)  // 3+ weeks at high utilization
        .OrderByDescending(g => g.Count());

    if (consistentlyMaxed.Any())
    {
      Console.WriteLine($"\nBURNOUT RISK (3+ weeks at 90%+ capacity):");
      foreach (var group in consistentlyMaxed.Take(5))
      {
        Console.WriteLine($"  {group.Key}: {group.Count()} weeks");
      }
    }
    Console.WriteLine("****************************\n");
  }

  /// <summary>
  /// Calculates statistics including total conflicts, people affected, and groupings by person/week.
  /// </summary>
  public void CalculateStatistics(ScheduleState state)
  {
    TotalConflictWeeks = Conflicts.Count;
    PeopleAffected = Conflicts.Select(c => c.PersonName).Distinct().Count();
    TotalOverloadHours = Conflicts.Sum(c => c.Overload);

    int totalAllocatedWeeks = state.PersonWeekGrid.Count;
    ConflictPercentage = totalAllocatedWeeks > 0
        ? (double)TotalConflictWeeks / totalAllocatedWeeks * 100
        : 0;

    // Group conflicts by person
    ConflictsByPerson = Conflicts
        .GroupBy(c => c.PersonName)
        .ToDictionary(g => g.Key, g => g.Count());

    // Group conflicts by week
    ConflictsByWeek = Conflicts
        .GroupBy(c => c.Week)
        .ToDictionary(g => g.Key, g => g.Count());
  }
}
/// <summary>
/// Represents a scheduling conflict where a person's allocated hours
/// exceed their weekly capacity in a specific week.
/// </summary>
public class Conflict
{
  public int PersonId { get; set; }
  public string PersonName { get; set; }
  public int Week { get; set; }
  public int TotalHours { get; set; }           // Total hours allocated
  public int Capacity { get; set; }             // Person's weekly capacity
  public int ProjectCount { get; set; }         // Number of projects
  public List<string> ProjectNames { get; set; }

  // Gets the severity of the overload (hours over capacity).

  public int Overload => TotalHours - Capacity;
}