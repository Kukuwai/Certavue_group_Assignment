using System;
using System.Collections.Generic;
using System.Linq;
using static GreedyAlg;
using static ScheduleState;

/// <summary>
/// Detects and reports scheduling capacity problems across the entire organization.
/// 
/// Functionality: Analyzes the schedule to identify two levels of concern - (1) Overloaded: when 
/// people's total assigned hours exceed their weekly capacity, and (2) High Utilization: when people 
/// are working at 90-100% of capacity with no buffer for urgent requests or unexpected issues.
/// 
/// Key Features: Tracks which specific people and weeks have problems, identifies which projects 
/// are contributing to overload, calculates severity metrics (total overload hours), and flags burnout 
/// risks when people consistently work at high capacity for multiple weeks.
/// 
/// Purpose: Helps managers make informed staffing decisions by providing early warnings before people 
/// become overloaded, identifying who has capacity for new work, and highlighting where schedule 
/// adjustments or additional resources are needed to prevent burnout and maintain sustainable workloads.
/// 
/// Output: Generates comprehensive ConflictReport objects containing detailed conflict entries, 
/// high utilization warnings, and calculated statistics grouped by person and week for easy analysis.
/// </summary>

public class ConflictDetector
{
  /// <summary>
  /// Gets the names of all projects a specific person is working on during a given week.
  /// 
  /// Input: Takes a ScheduleState (current schedule), personId (unique identifier), and week number.
  /// 
  /// Logic: Looks up the person by ID, then iterates through all their project assignments to find 
  /// which ones have allocated hours in the specified week. Only includes projects where the person 
  /// has more than 0 hours assigned for that week.
  /// 
  /// Purpose: Used by conflict analysis to show which specific projects are contributing to a person's 
  /// workload when they are overloaded or at high utilization, helping managers understand the source 
  /// of capacity issues.
  /// 
  /// Output: Returns a list of project names as strings. Returns empty list if person not found or 
  /// has no projects that week.
  /// </summary>
  private List<string> GetProjectsForPersonInWeek(ScheduleState state, int personId, int week)
  {
    // Initialize empty list to store project names
    var projects = new List<string>();
    // Look up the person by their ID in the state's people list
    var person = state.People.FirstOrDefault(p => p.id == personId);
    // Return empty list if person not found
    if (person == null) return projects;
    // Loop through all projects assigned to this person
    foreach (var projectEntry in person.projects)
    {// Get the project object (dictionary key)
      var project = projectEntry.Key;
      // Get the hours breakdown by week for this project (dictionary value)
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
  /// Analyzes the entire schedule to identify all capacity violations and high utilization warnings.
  /// 
  /// Input: Takes a ScheduleState object containing the current schedule with all people, projects, 
  /// and hour allocations.
  /// 
  /// Logic: Loops through every person-week entry in the PersonWeekGrid. For each entry, gets the 
  /// total hours allocated and compares against the person's capacity (defaulting to 40 hours if not set). 
  /// Flags two types of issues: (1) Conflicts - when total hours exceed capacity (overloaded), and 
  /// (2) High Utilization - when hours are between 90-100% of capacity (warning zone). For each flagged 
  /// issue, retrieves the specific project names contributing to the workload. Finally, calculates 
  /// summary statistics before returning.
  /// 
  /// Purpose: Serves as the main entry point for conflict detection. Provides users/managers with a complete 
  /// view of scheduling problems across the entire organization, helping identify who needs help, 
  /// which weeks are problematic, and which projects are causing capacity issues.
  /// 
  /// Output: Returns a ConflictReport object containing separate lists of overloaded and high utilization 
  /// entries, along with calculated statistics like total affected people, conflict percentages, and 
  /// groupings by person and week.
  /// </summary>
  public ConflictReport AnalyzeSchedule(ScheduleState state)
  {
    // Create empty report to store results
    var report = new ConflictReport();
    // Default capacity for people whose capacity isn't set in data
    const int DEFAULT_CAPACITY = 40;
    // Loop through every person-week combination in the schedule
    foreach (var kvp in state.PersonWeekGrid)
    {
      // Extract person ID, week number, and total hours allocated
      var personId = kvp.Key.PersonId;
      var week = kvp.Key.Week;
      var totalHours = kvp.Value;

      var person = state.People.FirstOrDefault(p => p.id == personId);
      if (person == null) continue;
      // Use person's actual capacity if set, otherwise use default 40 hours
      int capacity = person.capacity > 0 ? person.capacity : DEFAULT_CAPACITY;

      // Flag OVERLOADED (exceeds capacity)
      if (totalHours > capacity)
      {
        // Get the specific projects causing this overload
        var projectsThisWeek = GetProjectsForPersonInWeek(state, personId, week);
        // Create a conflict entry with all details
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
    // Calculate summary statistics from the collected conflicts
    report.CalculateStatistics(state);
    return report;
  }
}
/// <summary>
/// Stores the complete results of a schedule conflict analysis, including overloaded assignments 
/// and high utilization warnings.
/// 
/// Data: Contains two main lists - Conflicts (people working over capacity) and HighUtilization 
/// (people at 90-100% capacity). Also stores calculated statistics like total conflict weeks, 
/// number of people affected, overall conflict percentage, total overload hours, and breakdowns 
/// of conflicts grouped by person and by week.
/// 
/// Structure: Provides both raw detailed data (individual conflict entries with person, week, hours, 
/// and project information) and aggregated statistics (totals, percentages, groupings) to support 
/// different levels of analysis.
/// 
/// Purpose: Acts as the central data structure for conflict detection results throughout the program. 
/// Allows users/managers to understand not just what conflicts exist, but who is affected most, when problems 
/// occur, and how severe the issues are. Supports decision-making for resource reallocation, hiring 
/// needs, and schedule adjustments.
/// 
/// Methods: Includes CalculateStatistics() to compute summary metrics from the conflict data, and 
/// PrintReport() to display findings in a manager-friendly format with urgent items highlighted and 
/// burnout risks identified.
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