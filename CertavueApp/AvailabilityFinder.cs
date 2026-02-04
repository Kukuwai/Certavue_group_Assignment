using System;
using System.Collections.Generic;
using System.Linq;

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

  // Implemented the methods based on the Tests in FinderTest on 04 Feb 2026. 
  // This method gives all people working on a project and their weeks
  public Dictionary<string, List<int>> GetPeopleForProject(Project project)
  {
    var result = new Dictionary<string, List<int>>();

    foreach (var person in project.people)
    {
      if (person.projects.ContainsKey(project))
      {
        result[person.name] = person.projects[project];
      }
    }

    return result;
  }

  // This method gives all projects a person works on and their weeks
  public Dictionary<Project, List<int>> GetProjectForPerson(Person person)
  {
    return new Dictionary<Project, List<int>>(person.projects);
  }
}