using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;
using static Output;

public class TestOutput
{
[Fact]
public void TestAlgorithmToCsvAndHtml()
{
    string csvPath = GetTestCsvPath(); 
    var loader = new Loader();
    var (people, projects) = loader.LoadData(csvPath);
    var state = new ScheduleState(people, projects);


    new GreedyAlg().BuildGreedySchedule(state);

    var allAssignments = new List<AssignmentRow>();
    foreach (var person in state.People)
    {
        foreach (var proj in state.Projects)
        {
            var grid = state.GetGrid(proj, state.GetShift(proj));
            var personWeeks = grid.Where(k => k.PersonId == person.id).Select(k => k.Week).ToList();
            if (personWeeks.Any())
            {
                allAssignments.Add(new AssignmentRow {
                    PersonName = person.name,
                    ProjectName = proj.name,
                    StartWeek = personWeeks.Min(),
                    Duration = state.GetDuration(proj),
                    PeopleCount = proj.people.Count,
                    ActiveWeeks = personWeeks.ToHashSet()
                });
            }
        }
    }

    var sorted = allAssignments
        .OrderByDescending(a => a.Duration)
        .ThenByDescending(a => a.PeopleCount)
        .ToList();


    var program = new Program();
    program.ExportToHtml(csvPath, state); 
    program.ExportToCsv(csvPath, sorted); 

    string expectedCsvPath = csvPath.Replace(".csv", "_Sorted.csv");
    Assert.True(File.Exists(expectedCsvPath));
}

    private string GetTestCsvPath() 
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string projectRootDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        return Path.Combine(projectRootDir, "Data", "SmallTestSetRoles.csv");
    }
}