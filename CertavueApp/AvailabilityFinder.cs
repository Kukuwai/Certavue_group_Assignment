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
}