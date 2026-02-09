using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

public class TestAlg 
{
    [Fact]
    public void TestGreedyWithCsvData() 
    {
        var loader = new Loader();
        string testFilePath = "Data/SmallTestSetRoles.csv"; 
        var (people, projects) = loader.LoadData(testFilePath);

        var state = new ScheduleState(people, projects); 
        var greedy = new GreedyAlg();                    

        int startConflicts = state.PersonWeekGrid.Values.Count(v => v >= 2);

        greedy.BuildGreedySchedule(state);

        int finalConflicts = state.PersonWeekGrid.Values.Count(v => v >= 2);
        int totalSlots = state.PersonWeekGrid.Values.Count();
        int cleanSlots = state.PersonWeekGrid.Values.Count(v => v == 1);
        double successRate = totalSlots == 0 ? 100 : (double)cleanSlots / totalSlots * 100;

        Console.WriteLine("\n" + new string('=', 30));
        Console.WriteLine($"【Alg test report】");
        Console.WriteLine($"1. Initial Conflict Weeks: {startConflicts}");
        Console.WriteLine($"2. Remaining Conflicts After Optimization: {finalConflicts}");
        Console.WriteLine($"3. Final Success Rate: {successRate:0.##}%");
        Console.WriteLine(new string('=', 30) + "\n");
    }
}