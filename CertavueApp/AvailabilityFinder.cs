using System;
using System.Collections.Generic;
using System.Linq;
using static ScheduleState;
/// <summary>
/// Analyzes schedule state to find staffing availability and optimal scheduling opportunities.
/// 
/// Functionality: Searches for available people to staff new projects (by role, timeframe, headcount), 
/// identifies where new hires would add value by finding overloaded projects, discovers least busy weeks 
/// for timing new work, and queries individual or team availability patterns.
/// 
/// Purpose: Supports capacity planning by helping users/managers answer "Who can I assign?", "When should we 
/// schedule this?", "Where do we need help?", and "Who's free this week?".
/// 
/// Methods: FindPeopleForNewProject, FindWorkForNewPerson, FindLeastBusyWeeks, 
/// GetAvailablePeopleInWeek.
/// </summary>
public class AvailabilityFinder
{
  private ScheduleState _state;

  public AvailabilityFinder(ScheduleState state)
  {
    _state = state;
  }

  /// <summary>
  /// Finds all people who are completely free in a specific week.
  /// 
  /// Input: Week number to check.
  /// Logic: Filters through all people, summing their allocated hours from PersonWeekGrid for the specified week. Includes only people with zero total hours.
  /// Purpose: Helps managers quickly identify who is available to assign to urgent tasks or new projects in a particular week.
  /// Output: Returns list of Person objects who are free that week.
  /// </summary>
  public List<Person> GetAvailablePeopleInWeek(int week)
  {
    // Filter people to find those with zero hours allocated in the specified week
    return _state.People
        .Where(p =>
        {
          // Sum all hours allocated to this person for this week across all projects
          int totalHours = _state.PersonWeekGrid
              .Where(kvp => kvp.Key.PersonId == p.id && kvp.Key.Week == week)
              .Sum(kvp => kvp.Value);

          // Include only people with zero hours (completely free)
          return totalHours == 0;
        })
        .ToList();
  }
  /// <summary>
  /// Finds the weeks with the lightest overall workload for optimal project scheduling.
  /// 
  /// Input: Number of weeks to return (defaults to 5).
  /// Logic: Loops through all 52 weeks, summing total allocated hours across all people from PersonWeekGrid. Sorts weeks by total hours ascending and returns the requested number of least busy weeks.
  /// Purpose: Helps managers identify the best time windows to schedule new projects when team capacity is highest.
  /// Output: Returns list of week numbers ordered from least to most busy (limited to requested count).
  /// </summary>
  public List<int> FindLeastBusyWeeks(int numberOfWeeks = 5)
  {
    // Track total workload for each week
    var weekWorkload = new Dictionary<int, int>();

    // Calculate total hours allocated across all people for each week
    for (int week = 1; week <= 52; week++)
    {
      // Sum all hours allocated in this week across entire team
      int totalHours = _state.PersonWeekGrid
          .Where(kvp => kvp.Key.Week == week)
          .Sum(kvp => kvp.Value);

      weekWorkload[week] = totalHours;
    }

    // Sort weeks by workload (ascending) and return the least busy ones
    return weekWorkload
        .OrderBy(kvp => kvp.Value)
        .Take(numberOfWeeks)
        .Select(kvp => kvp.Key)
        .ToList();
  }

  /// <summary>
  /// Identifies work opportunities for a new hire by finding overloaded projects.
  /// 
  /// Input: New person's name and number of weeks they're available (defaults to 52).
  /// Logic: Scans all weeks to find people working at/over capacity (defaults to 40h). For each overloaded person, identifies which projects they're working on that week. Groups opportunities by project and ranks them by number of weeks needing help.
  /// Purpose: Helps managers determine where a new hire would be most valuable by showing which projects have the most capacity constraints.
  /// Output: Returns NewPersonWorkResult with top 10 projects needing help, showing which weeks need support and total opportunity count.
  /// </summary>
  public NewPersonWorkResult FindWorkForNewPerson(string personName, int availableWeeks = 52)
  {
    // Initialize result object with new person's details
    var result = new NewPersonWorkResult
    {
      PersonName = personName,
      AvailableWeeks = availableWeeks
    };

    // Default capacity for people whose capacity isn't set in data
    const int DEFAULT_CAPACITY = 40;

    // Track which weeks have overloaded people
    var overloadedWeeks = new Dictionary<int, List<Person>>();

    // Scan all weeks to find overloaded people
    for (int week = 1; week <= availableWeeks; week++)
    {
      var overloadedPeople = _state.People
          .Where(p =>
          {
            // Sum total hours allocated to this person this week
            int totalHours = _state.PersonWeekGrid
                  .Where(kvp => kvp.Key.PersonId == p.id && kvp.Key.Week == week)
                  .Sum(kvp => kvp.Value);

            // Use person's actual capacity if set, otherwise use default 40 hours
            int capacity = p.capacity > 0 ? p.capacity : DEFAULT_CAPACITY;

            // Flag as overloaded if hours meet or exceed capacity
            return totalHours >= capacity;
          })
          .ToList();

      // Store overloaded people for this week
      if (overloadedPeople.Count > 0)
        overloadedWeeks[week] = overloadedPeople;
    }

    // Track which projects need help in which weeks
    var opportunitiesByProject = new Dictionary<string, List<int>>();

    // For each week with overloaded people, find which projects are involved
    foreach (var (week, overloadedPeople) in overloadedWeeks)
    {
      foreach (var person in overloadedPeople)
      {
        // Check all projects this person is working on
        foreach (var projectEntry in person.projects)
        {
          var project = projectEntry.Key;
          var weekHours = projectEntry.Value;

          // Skip if person not working on this project this week
          if (!weekHours.ContainsKey(week)) continue;

          // Add project to opportunities list if not already there
          if (!opportunitiesByProject.ContainsKey(project.name))
            opportunitiesByProject[project.name] = new List<int>();

          // Add this week to the project's list of needy weeks (avoid duplicates)
          if (!opportunitiesByProject[project.name].Contains(week))
            opportunitiesByProject[project.name].Add(week);
        }
      }
    }

    // Rank projects by number of weeks they need help, take top 10
    result.WorkOpportunities = opportunitiesByProject
        .OrderByDescending(kvp => kvp.Value.Count)
        .Take(10)
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.OrderBy(w => w).ToList());

    // Calculate summary statistics
    result.TotalWeeksAvailable = result.WorkOpportunities.Values.Sum(weeks => weeks.Count);
    result.ProjectsNeedingHelp = result.WorkOpportunities.Count;

    return result;
  }
  /// <summary>
  /// Searches for available people to staff a new project based on role, timeframe, and headcount needs.
  /// 
  /// Input: Start week, duration, number of people needed, and optional role filter.
  /// Logic: Filters people by role (case-insensitive), then checks each person's availability week-by-week by comparing allocated hours against capacity (defaults to 40h). Classifies people as fully available (free all weeks) or partially available (free some weeks), then calculates if enough coverage exists.
  /// Purpose: Helps managers quickly identify who can be assigned to new projects and whether sufficient staffing is available.
  /// Output: Returns NewProjectStaffingResult with lists of fully/partially available people, week coverage analysis, and feasibility assessment.
  /// </summary>
  public NewProjectStaffingResult FindPeopleForNewProject(int startWeek, int duration, int peopleNeeded, string requiredRole = "")
  {
    // Initialize result object with project parameters
    var result = new NewProjectStaffingResult
    {
      StartWeek = startWeek,
      Duration = duration,
      PeopleNeeded = peopleNeeded,
      EndWeek = startWeek + duration - 1,
      RequiredRole = requiredRole
    };

    // Default capacity for people whose capacity isn't set in data
    const int DEFAULT_CAPACITY = 40;

    // Filter by role if specified (case-insensitive match)
    var peopleToCheck = string.IsNullOrEmpty(requiredRole)
        ? _state.People
        : _state.People.Where(p => p.role.Equals(requiredRole, StringComparison.OrdinalIgnoreCase)).ToList();

    // Check each person's availability week by week
    foreach (var person in peopleToCheck)
    {
      // Use person's actual capacity if set, otherwise use default 40 hours
      int capacity = person.capacity > 0 ? person.capacity : DEFAULT_CAPACITY;

      bool isFreeForAllWeeks = true;
      var availableWeeks = new List<int>();

      // Loop through each week of the project duration
      for (int week = startWeek; week < startWeek + duration; week++)
      {
        // Sum all hours already allocated to this person across all projects for this week
        int allocatedHours = _state.PersonWeekGrid
            .Where(kvp => kvp.Key.PersonId == person.id && kvp.Key.Week == week)
            .Sum(kvp => kvp.Value);

        // Check if person has remaining capacity (not at/over limit)
        if (allocatedHours < capacity)
        {
          availableWeeks.Add(week);
        }
        else
        {
          isFreeForAllWeeks = false;
        }
      }

      // Add to fully available list if free for entire project duration
      if (isFreeForAllWeeks)
      {
        result.FullyAvailablePeople.Add(person);
      }

      // Add to partially available list if free for at least some weeks
      if (availableWeeks.Any())
      {
        result.PartiallyAvailablePeople[person] = availableWeeks;
      }
    }

    // Calculate which weeks have at least one person available (coverage analysis)
    var coveredWeeksSet = new HashSet<int>();
    foreach (var weeksList in result.PartiallyAvailablePeople.Values)
    {
      foreach (var week in weeksList)
      {
        coveredWeeksSet.Add(week);
      }
    }

    result.CoveredWeeks = coveredWeeksSet.OrderBy(w => w).ToList();

    // Find weeks with no available people
    result.UncoveredWeeks = Enumerable.Range(startWeek, duration)
        .Except(coveredWeeksSet)
        .ToList();

    // Determine if project can be fulfilled: either enough fully available people, or full week coverage with enough partial people
    result.CanBeFulfilled =
        (result.FullyAvailablePeople.Count >= peopleNeeded) ||
        (result.CoveredWeeks.Count == duration && result.PartiallyAvailablePeople.Count >= peopleNeeded);

    return result;
  }

}

/// <summary>
/// Contains the results of searching for work opportunities for a new person,
/// identifying overloaded projects and the specific weeks where additional support is needed,
/// helping to determine where a new hire could provide the most value to the schedule.
/// </summary>
public class NewPersonWorkResult
{
  public string PersonName { get; set; } = string.Empty;
  public int AvailableWeeks { get; set; }
  public Dictionary<string, List<int>> WorkOpportunities { get; set; } = new Dictionary<string, List<int>>();
  public int TotalWeeksAvailable { get; set; }
  public int ProjectsNeedingHelp { get; set; }

  //Prints a formatted summary of work opportunities to the console.
  public void PrintSummary()
  {
    Console.WriteLine($"\n******* WORK OPPORTUNITIES FOR {PersonName} *******");
    Console.WriteLine($"Available for: {AvailableWeeks} weeks");
    Console.WriteLine($"Projects needing help: {ProjectsNeedingHelp}");
    Console.WriteLine($"Total work opportunities: {TotalWeeksAvailable} person-weeks");



    if (WorkOpportunities.Count > 0)
    {
      Console.WriteLine($"\nTop projects where {PersonName} could help:");
      foreach (var (projectName, weeks) in WorkOpportunities.Take(5))
      {
        Console.WriteLine($"\n  {projectName}:");
        Console.WriteLine($"    Weeks needed: {string.Join(", ", weeks.Take(10))}");
        Console.WriteLine($"    Total: {weeks.Count} weeks");


      }

      if (WorkOpportunities.Count > 5)
      {
        Console.WriteLine($"\n  ... and {WorkOpportunities.Count - 5} more projects");
      }


    }
    else
    {
      Console.WriteLine("\n  No overloaded projects found - schedule is well balanced!");

    }

    Console.WriteLine("*******************\n");
  }
}
/// <summary>
/// Contains the results of searching for available staff for a new project,
/// including fully and partially available people, week-by-week coverage analysis,
/// and whether the project can be fulfilled with the required number of people.
/// </summary>
public class NewProjectStaffingResult
{
  public int StartWeek { get; set; }
  public int Duration { get; set; }
  public int EndWeek { get; set; }
  public int PeopleNeeded { get; set; }
  public string RequiredRole { get; set; } = string.Empty;
  public List<Person> FullyAvailablePeople { get; set; } = new List<Person>();
  public Dictionary<Person, List<int>> PartiallyAvailablePeople { get; set; } = new Dictionary<Person, List<int>>();
  public List<int> CoveredWeeks { get; set; } = new List<int>();
  public List<int> UncoveredWeeks { get; set; } = new List<int>();
  public bool CanBeFulfilled { get; set; }

  /// Prints a summary of the staffing search results to the console.
  public void PrintSummary()
  {
    Console.WriteLine($"\n********* NEW PROJECT STAFFING ********");
    Console.WriteLine($"Project: Weeks {StartWeek}-{EndWeek} ({Duration} weeks)");
    Console.WriteLine($"Role: {(string.IsNullOrEmpty(RequiredRole) ? "Any" : RequiredRole)}");
    Console.WriteLine($"People needed: {PeopleNeeded}");
    Console.WriteLine($"Can be fulfilled: {(CanBeFulfilled ? " YES" : "NO")}");



    Console.WriteLine($"\nFully available: {FullyAvailablePeople.Count}");
    foreach (var person in FullyAvailablePeople.Take(5))
    {
      Console.WriteLine($"  - {person.name}");
    }

    Console.WriteLine($"\nPartially available: {PartiallyAvailablePeople.Count}");
    foreach (var (person, weeks) in PartiallyAvailablePeople.Take(5))
    {
      Console.WriteLine($"  - {person.name}: {weeks.Count} weeks");

    }

    Console.WriteLine($"\nCoverage: {CoveredWeeks.Count}/{Duration} weeks");
    if (UncoveredWeeks.Any())
    {
      Console.WriteLine($"Uncovered: {string.Join(", ", UncoveredWeeks)}");

    }

    Console.WriteLine("***************************\n");
  }
}
