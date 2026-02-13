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
  // This method gives the number of projects a person is working in a week.
  public int GetPersonWorkload(string personName, int week)
  {
    var person = _state.People.FirstOrDefault(p => p.name == personName);
    if (person == null) return -1;

    var key = new ScheduleState.WeekKey(person.id, week);

    if (_state.PersonWeekGrid.ContainsKey(key))
    {
      return _state.PersonWeekGrid[key];
    }
    return 0;
  }
  // This method gives a list of Person objects which are free in a given week. 
  public List<Person> GetAvailablePeopleInWeek(int week)
  {
    return _state.People
        .Where(p =>
        {
          var key = new ScheduleState.WeekKey(p.id, week);
          return !_state.PersonWeekGrid.ContainsKey(key) || _state.PersonWeekGrid[key] == 0;
        })
        .ToList();
  }
  // This method gives all weeks where a given person is free or not working.
  public List<int> GetAvailableWeeksForPerson(string personName)
  {
    var person = _state.People.FirstOrDefault(p => p.name == personName);
    if (person == null) return new List<int>();

    var availableWeeks = new List<int>();
    for (int week = 1; week <= 52; week++)
    {
      var key = new ScheduleState.WeekKey(person.id, week);
      if (!_state.PersonWeekGrid.ContainsKey(key) || _state.PersonWeekGrid[key] == 0)
      {
        availableWeeks.Add(week);
      }
    }
    return availableWeeks;
  }

  // This method gives an ordered list of weeks starting with least number of projects.
  public List<int> FindLeastBusyWeeks(int numberOfWeeks = 5)
  {
    var weekWorkload = new Dictionary<int, int>();

    // Calculate total assignments per week
    for (int week = 1; week <= 52; week++)
    {
      int totalWork = _state.PersonWeekGrid
          .Where(kv => kv.Key.Week == week)
          .Sum(kv => kv.Value);

      weekWorkload[week] = totalWork;
      Console.WriteLine($"Week {week}: {weekWorkload[week]} total assignments");
    }

    // Return least busy weeks
    return weekWorkload
        .OrderBy(kv => kv.Value)
        .Take(numberOfWeeks)
        .Select(kv => kv.Key)
        .ToList();
  }
  // This method gives number of weeks for a person working on more than one project that means the person is overloaded.
  public int CountOverloadedWeeks(string personName)
  {
    var person = _state.People.FirstOrDefault(p => p.name == personName);
    if (person == null) return 0;

    int overloadedWeeks = 0;
    for (int week = 1; week <= 52; week++)
    {
      var key = new ScheduleState.WeekKey(person.id, week);
      if (_state.PersonWeekGrid.ContainsKey(key) && _state.PersonWeekGrid[key] > 1)
      {
        overloadedWeeks++;
      }
    }
    return overloadedWeeks;
  }
  // Find what work a new person could take on;
  public NewPersonWorkResult FindWorkForNewPerson(string personName, int availableWeeks = 52)
  {
    var result = new NewPersonWorkResult
    {
      PersonName = personName,
      AvailableWeeks = availableWeeks

    };

    // Find weeks where people are overloaded (2+ projects);

    var overloadedWeeks = new Dictionary<int, List<Person>>();

    for (int week = 1; week <= 52; week++)
    {
      var overloadedPeople = _state.People
          .Where(p =>
          {
            var key = new ScheduleState.WeekKey(p.id, week);
            return _state.PersonWeekGrid.ContainsKey(key) && _state.PersonWeekGrid[key] > 1;
          })
          .ToList();

      if (overloadedPeople.Count > 0)
      {
        overloadedWeeks[week] = overloadedPeople;
      }

    }

    // Find projects during overloaded weeks that new person could help with;
    var opportunitiesByProject = new Dictionary<string, List<int>>();

    foreach (var (week, overloadedPeople) in overloadedWeeks)
    {
      // Find projects these overloaded people are working on;
      foreach (var person in overloadedPeople)
      {
        foreach (var project in _state.Projects)
        {
          if (!project.people.Contains(person)) continue;

          // Check if person works on this project this week;
          var shift = _state.GetShift(project);
          var footprint = _state.GetGrid(project, shift);

          if (footprint.Any(f => f.PersonId == person.id && f.Week == week))
          {
            // This project needs help this week;
            if (!opportunitiesByProject.ContainsKey(project.name))
            {
              opportunitiesByProject[project.name] = new List<int>();
            }

            if (!opportunitiesByProject[project.name].Contains(week))
            {
              opportunitiesByProject[project.name].Add(week);
            }
          }
        }
      }
    }

    // Sort projects by number of weeks they need help
    result.WorkOpportunities = opportunitiesByProject
        .OrderByDescending(kvp => kvp.Value.Count)
        .Take(10)  // Top 10 projects that need most help
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.OrderBy(w => w).ToList());

    result.TotalWeeksAvailable = result.WorkOpportunities.Values.Sum(weeks => weeks.Count);
    result.ProjectsNeedingHelp = result.WorkOpportunities.Count;

    return result;
  }
  // Find available people for a new project
  public NewProjectStaffingResult FindPeopleForNewProject(int startWeek, int duration, int peopleNeeded)
  {
    var result = new NewProjectStaffingResult
    {
      StartWeek = startWeek,
      Duration = duration,
      PeopleNeeded = peopleNeeded,
      EndWeek = startWeek + duration - 1
    };

    // Find people who are free for ALL weeks of the project
    var availableForAllWeeks = new List<Person>();



    foreach (var person in _state.People)
    {
      bool isFreeForAllWeeks = true;

      // Check each week of the project
      for (int week = startWeek; week < startWeek + duration; week++)
      {
        var key = new ScheduleState.WeekKey(person.id, week);

        // If person is busy in ANY week, they can't do the full project
        if (_state.PersonWeekGrid.ContainsKey(key) && _state.PersonWeekGrid[key] > 0)
        {
          isFreeForAllWeeks = false;
          break;
        }
      }

      if (isFreeForAllWeeks)
      {
        availableForAllWeeks.Add(person);
      }
    }

    result.AvailablePeople = availableForAllWeeks;
    result.CanBeFulfilled = availableForAllWeeks.Count >= peopleNeeded;

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
  public List<Person> AvailablePeople { get; set; } = new List<Person>();
  public bool CanBeFulfilled { get; set; }

  public void PrintSummary()
  {
    Console.WriteLine($"\n********** NEW PROJECT STAFFING ********");
    Console.WriteLine($"Project: Weeks {StartWeek}-{EndWeek} ({Duration} weeks)");
    Console.WriteLine($"People needed: {PeopleNeeded}");
    Console.WriteLine($"People available: {AvailablePeople.Count}");
    Console.WriteLine($"Can be fulfilled: {(CanBeFulfilled ? " YES" : "NO")}");



    if (AvailablePeople.Count > 0)
    {
      Console.WriteLine($"\nAvailable people:");
      foreach (var person in AvailablePeople.Take(10))
      {
        Console.WriteLine($"  - {person.name}");
      }
      if (AvailablePeople.Count > 10)
      {
        Console.WriteLine($"  ... and {AvailablePeople.Count - 10} more");


      }
    }
    else
    {
      Console.WriteLine("\n  No people available for this timeframe.");
    }
    Console.WriteLine("***************************\n");
  }
}
