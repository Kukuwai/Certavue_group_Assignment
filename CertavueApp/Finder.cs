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

  // 1. Find which weeks a specific person is available
  public List<int> GetAvailableWeeksForPerson(string personName, int startWeek = 1, int endWeek = 52)
  {
    // var person = _state.People.FirstOrDefault(p => p.name == personName);
    // if (person == null)
    // {
    //     Console.WriteLine($"Person {personName} not found!");
    //     return new List<int>();
    // }

    var person = _state.People.FirstOrDefault(p => p.name == personName);
    if (person == null)
    {
      console.W
    }

  }
}
