using System;
using System.Collections.Generic;
using System.Linq;
public class ConflictDetector
{
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
          PersonName = $"Person_{personId:D2}", // Temporary placeholder
          Week = week,
          ProjectCount = count,
          ProjectNames = new List<string> { "Unknown" } // Temporary
        });
      }
    }

    return report;
  }
}
public class ConflictReport
{
  public List<Conflict> Conflicts { get; set; } = new List<Conflict>();

  public void PrintReport()
  {
    Console.WriteLine($"\nTotal conflicts found: {Conflicts.Count}");
    foreach (var conflict in Conflicts)
    {
      Console.WriteLine($"{conflict.PersonName}, Week {conflict.Week}: {conflict.ProjectCount} projects");
    }
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