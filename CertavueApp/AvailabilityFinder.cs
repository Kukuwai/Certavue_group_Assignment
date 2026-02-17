using System;
using System.Collections.Generic;
using System.Linq;
using static ScheduleState;

public class AvailabilityFinder
{
  private ScheduleState _state;

  public AvailabilityFinder(ScheduleState state)
  {
    _state = state;
  }
  //   // This method gives the number of projects a person is working in a week.
  //   public int GetPersonWorkload(string personName, int week)
  //   {
  //     var person = _state.People.FirstOrDefault(p => p.name == personName);
  //     if (person == null) return -1;

  //     var key = new ScheduleState.WeekKey(person.id, week);

  //     if (_state.PersonWeekGrid.ContainsKey(key))
  //     {
  //       return _state.PersonWeekGrid[key];
  //     }
  //     return 0;
  //   }
  //   // This method gives a list of Person objects which are free in a given week. 
  //   public List<Person> GetAvailablePeopleInWeek(int week)
  //   {
  //     return _state.People
  //         .Where(p =>
  //         {
  //           var key = new ScheduleState.WeekKey(p.id, week);
  //           return !_state.PersonWeekGrid.ContainsKey(key) || _state.PersonWeekGrid[key] == 0;
  //         })
  //         .ToList();
  //   }
  //   // This method gives all weeks where a given person is free or not working.
  //   public List<int> GetAvailableWeeksForPerson(string personName)
  //   {
  //     var person = _state.People.FirstOrDefault(p => p.name == personName);
  //     if (person == null) return new List<int>();

  //     var availableWeeks = new List<int>();
  //     for (int week = 1; week <= 52; week++)
  //     {
  //       var key = new ScheduleState.WeekKey(person.id, week);
  //       if (!_state.PersonWeekGrid.ContainsKey(key) || _state.PersonWeekGrid[key] == 0)
  //       {
  //         availableWeeks.Add(week);
  //       }
  //     }
  //     return availableWeeks;
  //   }

  //   // This method gives an ordered list of weeks starting with least number of projects.
  //   public List<int> FindLeastBusyWeeks(int numberOfWeeks = 5)
  //   {
  //     var weekWorkload = new Dictionary<int, int>();

  //     // Calculate total assignments per week
  //     for (int week = 1; week <= 52; week++)
  //     {
  //       int totalWork = _state.PersonWeekGrid
  //           .Where(kv => kv.Key.Week == week)
  //           .Sum(kv => kv.Value);

  //       weekWorkload[week] = totalWork;
  //       Console.WriteLine($"Week {week}: {weekWorkload[week]} total assignments");
  //     }

  //     // Return least busy weeks
  //     return weekWorkload
  //         .OrderBy(kv => kv.Value)
  //         .Take(numberOfWeeks)
  //         .Select(kv => kv.Key)
  //         .ToList();
  //   }
  //   // This method gives number of weeks for a person working on more than one project that means the person is overloaded.
  //   public int CountOverloadedWeeks(string personName)
  //   {
  //     var person = _state.People.FirstOrDefault(p => p.name == personName);
  //     if (person == null) return 0;

  //     int overloadedWeeks = 0;
  //     for (int week = 1; week <= 52; week++)
  //     {
  //       var key = new ScheduleState.WeekKey(person.id, week);
  //       if (_state.PersonWeekGrid.ContainsKey(key) && _state.PersonWeekGrid[key] > 1)
  //       {
  //         overloadedWeeks++;
  //       }
  //     }
  //     return overloadedWeeks;
  //   }


  /// <summary>
  /// Scans the current schedule state to find overloaded weeks where people exceed capacity,
  /// and returns the top projects where a new person could provide additional support.
  /// </summary>
  public NewPersonWorkResult FindWorkForNewPerson(string personName, int availableWeeks = 52)
  {
    var result = new NewPersonWorkResult
    {
      PersonName = personName,
      AvailableWeeks = availableWeeks
    };

    var overloadedWeeks = new Dictionary<int, List<Person>>();

    for (int week = 1; week <= availableWeeks; week++)
    {
      var overloadedPeople = _state.People
          .Where(p =>
          {
            // Read from ScheduleState.
            int totalHours = _state.PersonWeekGrid
                  .Where(kvp => kvp.Key.PersonId == p.id && kvp.Key.Week == week)
                  .Sum(kvp => kvp.Value);

            return totalHours >= p.capacity;
          })
          .ToList();

      if (overloadedPeople.Count > 0)
        overloadedWeeks[week] = overloadedPeople;
    }

    var opportunitiesByProject = new Dictionary<string, List<int>>();

    foreach (var (week, overloadedPeople) in overloadedWeeks)
    {
      foreach (var person in overloadedPeople)
      {
        foreach (var projectEntry in person.projects)
        {
          var project = projectEntry.Key;
          var weekHours = projectEntry.Value;

          if (!weekHours.ContainsKey(week)) continue;

          if (!opportunitiesByProject.ContainsKey(project.name))
            opportunitiesByProject[project.name] = new List<int>();

          if (!opportunitiesByProject[project.name].Contains(week))
            opportunitiesByProject[project.name].Add(week);
        }
      }
    }

    result.WorkOpportunities = opportunitiesByProject
        .OrderByDescending(kvp => kvp.Value.Count)
        .Take(10)
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.OrderBy(w => w).ToList());

    result.TotalWeeksAvailable = result.WorkOpportunities.Values.Sum(weeks => weeks.Count);
    result.ProjectsNeedingHelp = result.WorkOpportunities.Count;

    return result;
  }

  /// <summary>
  /// Searches the current schedule state to find people available for a new project,
  /// returning fully and partially available staff based on role, start week, duration, and headcount needed.
  /// </summary>
  public NewProjectStaffingResult FindPeopleForNewProject(int startWeek, int duration, int peopleNeeded, string requiredRole = "")
  {
    var result = new NewProjectStaffingResult
    {
      StartWeek = startWeek,
      Duration = duration,
      PeopleNeeded = peopleNeeded,
      EndWeek = startWeek + duration - 1,
      RequiredRole = requiredRole
    };

    // Filter by role if specified (CASE-INSENSITIVE)
    var peopleToCheck = string.IsNullOrEmpty(requiredRole)
        ? _state.People
        : _state.People.Where(p => p.role.Equals(requiredRole, StringComparison.OrdinalIgnoreCase)).ToList();

    // Check each person's availability
    foreach (var person in peopleToCheck)
    {
      bool isFreeForAllWeeks = true;
      var availableWeeks = new List<int>();

      for (int week = startWeek; week < startWeek + duration; week++)
      {
        // Sum all hours allocated to this person in this week across ALL projects
        int allocatedHours = _state.PersonWeekGrid
            .Where(kvp => kvp.Key.PersonId == person.id && kvp.Key.Week == week)
            .Sum(kvp => kvp.Value);

        // Check if person has remaining capacity
        if (allocatedHours < person.capacity)
        {
          availableWeeks.Add(week);
        }
        else
        {
          isFreeForAllWeeks = false;
        }
      }

      if (isFreeForAllWeeks)
      {
        result.FullyAvailablePeople.Add(person);
      }

      if (availableWeeks.Any())
      {
        result.PartiallyAvailablePeople[person] = availableWeeks;
      }
    }

    // Calculate coverage
    var coveredWeeksSet = new HashSet<int>();
    foreach (var weeksList in result.PartiallyAvailablePeople.Values)
    {
      foreach (var week in weeksList)
      {
        coveredWeeksSet.Add(week);
      }
    }

    result.CoveredWeeks = coveredWeeksSet.OrderBy(w => w).ToList();
    result.UncoveredWeeks = Enumerable.Range(startWeek, duration)
        .Except(coveredWeeksSet)
        .ToList();

    // Determine if can be fulfilled
    result.CanBeFulfilled =
        (result.FullyAvailablePeople.Count >= peopleNeeded) ||
        (result.CoveredWeeks.Count == duration && result.PartiallyAvailablePeople.Count >= peopleNeeded);

    return result;
  }

}
public class NewPersonWorkResult
{
  public string PersonName { get; set; } = string.Empty;
  public int AvailableWeeks { get; set; }
  public Dictionary<string, List<int>> WorkOpportunities { get; set; } = new Dictionary<string, List<int>>();
  public int TotalWeeksAvailable { get; set; }
  public int ProjectsNeedingHelp { get; set; }

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

    Console.WriteLine("****************\n");
  }
}
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

  public void PrintSummary()
  {
    Console.WriteLine($"\n********** NEW PROJECT STAFFING ********");
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
