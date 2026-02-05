﻿using static Project;
using static Person;
using static Loader;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using static MoveByConflict;
using static ScheduleState;
using System.IO;
using System.Linq;
using System.Text;

public class Program
{
    List<Project> projects = new List<Project>();
    List<Person> people = new List<Person>();



    public Program()
    {
        // loadData();
        // var stateAfter = new GreedyAlg().StartGreedy(people, projects);

        // // string filePath = "Data/schedule_target75_large.csv";
        // ExportToHtml(filePath, stateAfter);

        // var m = new MoveByConflict(stateAfter);

        // Before Greedy algorithm
        //Console.WriteLine("********* Before running Greedy *********");
        // var stateBefore = new ScheduleState(people, projects);
        // var detectorBefore = new ConflictDetector();
        // var reportBefore = detectorBefore.AnalyzeSchedule(stateBefore);

        // reportBefore.CalculateStatistics(stateBefore);
        //reportBefore.PrintReport();

        // Run Greedy algorithm

        // Console.WriteLine("********* Running Greedy ***************");
        var originalState = loadData();
        testPrint(originalState);
        var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
        testAlgo(scheduleAfterGreedy);
        var scheduleAfterConflict = new MoveByConflict().start(scheduleAfterGreedy, projects);
        testAlgo(scheduleAfterConflict);



        // After Greedy algorithm
        // Console.WriteLine("********* After running Greedy *******");
        // var detectorAfter = new ConflictDetector();
        // var reportAfter = detectorAfter.AnalyzeSchedule(stateAfter);
        // reportAfter.CalculateStatistics(stateAfter);
        //reportAfter.PrintReport();

        // Comparison
        // Console.WriteLine("************ Comparison *************");
        // Console.WriteLine($"Conflicts before: {reportBefore.TotalConflictWeeks}");
        // Console.WriteLine($"Conflicts after:  {reportAfter.TotalConflictWeeks}");
        // Console.WriteLine($"Reduction:        {reportBefore.TotalConflictWeeks - reportAfter.TotalConflictWeeks}");
        // Console.WriteLine($"% Improvement:    {(1 - (double)reportAfter.TotalConflictWeeks / reportBefore.TotalConflictWeeks) * 100:F1}%");
        // //testPrint();

        //var beforeGreedy = new ScheduleState(people, projects);
        // GreedyChecker("Before Greedy", beforeGreedy);
        //new GreedyAlg().StartGreedy(people, projects);
        //var afterGreedy = new ScheduleState(people, projects);
        //  GreedyChecker("After Greedy", afterGreedy);
        //var state = new ScheduleState(people, projects);
        //var detector = new ConflictDetector();
        //var report = detector.AnalyzeSchedule(state);
        //report.CalculateStatistics(state);

        //Console.WriteLine($"Total conflicts: {report.TotalConflictWeeks}");
        //Console.WriteLine($"People affected: {report.PeopleAffected}");
        //Console.WriteLine($"Conflict rate: {report.ConflictPercentage:F2}%");

        // Console.WriteLine("\nTop 3 conflicted people:");
        // foreach (var person in report.ConflictsByPerson.OrderByDescending(kv => kv.Value).Take(3))
        // {
        //     Console.WriteLine($"  {person.Key}: {person.Value} conflicts");
        // }
        
    }

    /*public void StartApp()
    {
        loadData(); // 加载大数据
        var stateAfter = new GreedyAlg().StartGreedy(people, projects);
        string filePath = "Data/schedule_target75_large.csv";
        ExportToHtml(filePath, stateAfter);
        //var m = new MoveByConflict(stateAfter);
    }*/

    public ScheduleState loadData()
    {
        Loader load = new Loader();
        (var people, var projects) = load.LoadData("Data/schedule_target75_medium_with_roles_40s.csv");
        var state = new ScheduleState(people, projects);
        this.people = people;
        this.projects = projects;
        Console.WriteLine("Loaded.");
        return state;
    }


    public void ExportToCsv(string originalFileName, List<AssignmentRow> sortedData)
    {
        try
        {
            string exportPath = originalFileName.Replace(".csv", "_Sorted.csv");
            var csv = new StringBuilder();

            // 1. 写入表头 (包含排序因子，方便你检查顺序)
            csv.AppendLine("Person,Project,StartWeek,Duration,PeopleCount,W1,W52_Check");

            // 2. 写入数据行
            foreach (var row in sortedData)
            {
                // 构造周数据的简易视图 (这里只展示是否有活，或者你可以根据需要扩展)
                string workStatus = row.ActiveWeeks.Count > 0 ? "Active" : "Idle";
                
                var line = string.Format("{0},{1},{2},{3},{4},{5}",
                    row.PersonName,
                    row.ProjectName,
                    row.StartWeek,
                    row.Duration,
                    row.PeopleCount,
                    workStatus);
                
                csv.AppendLine(line);
            }

            File.WriteAllText(exportPath, csv.ToString(), Encoding.UTF8);
            Console.WriteLine($"\n[Success] csv had saved at: {exportPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("CSV export failed: " + ex.Message);
        }
    }



    public void ExportToHtml(string originalFileName, ScheduleState state)
    {
        try
        {
            // generate the HTML filename based on the input CSV filename
            string exportPath = originalFileName.Replace(".csv", "_Heatmap.html");
            var sb = new StringBuilder();

            //  CSS outlook 
            sb.Append("<html><head><meta charset='UTF-8'><style>");
            sb.Append("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; padding: 20px; }");
            sb.Append("h2 { color: #2c3e50; text-align: center; }");
            sb.Append("table { border-collapse: collapse; background: white; box-shadow: 0 4px 15px rgba(0,0,0,0.1); width: 100%; margin-top: 20px; }");
            sb.Append("th { background-color: #2c3e50; color: white; padding: 12px 8px; position: sticky; top: 0; z-index: 10; font-size: 12px; }");
            sb.Append("td { border: 1px solid #eee; padding: 8px; text-align: center; font-size: 11px; }");
            sb.Append(".name-col { text-align: left; font-weight: bold; background: #ebf0f1; position: sticky; left: 0; z-index: 5; min-width: 120px; }");
            sb.Append(".proj-col { text-align: left; background: #fff; position: sticky; left: 120px; z-index: 4; border-right: 2px solid #ddd; min-width: 150px; }");
            sb.Append(".work { background-color: #3498db; color: white; font-weight: bold; }");
            sb.Append("tr:hover { background-color: #f1f1f1; }");
            sb.Append("</style></head><body>");

            sb.Append("<h2>Project Scheduling.</h2>");
            sb.Append("<table><tr><th class='name-col'>Person</th><th class='proj-col'>Project</th>");
            for (int w = 1; w <= 52; w++) sb.Append($"<th>W{w}</th>");
            sb.Append("</tr>");

            // 2. align on Alg logic to sort
            var allAssignments = new List<AssignmentRow>();

            foreach (var person in state.People)
            {
                foreach (var proj in state.Projects)
                {
                
                    var grid = state.GetGrid(proj, state.GetShift(proj));
                    
                    // Filter out the weeks assigned to the current person
                    var personWeeks = grid.Where(k => k.PersonId == person.id).Select(k => k.Week).ToList();

                    if (personWeeks.Any())
                    {
                        allAssignments.Add(new AssignmentRow
                        {
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

        
            // Sort by duration descending; if durations are equal, sort by headcount descending
            var sortedAssignments = allAssignments
                .OrderByDescending(a => a.Duration)   // align on Alg method：OrderByDescending(p => state.GetDuration(p))
                .ThenByDescending(a => a.PeopleCount) // same with Alg method：ThenByDescending(p => p.people.Count)
                .ThenBy(a => a.StartWeek)            
                .ThenBy(a => a.PersonName)            
                .ToList();

            // 4. create html table
            foreach (var row in sortedAssignments)
            {
                sb.Append("<tr>");
                sb.Append($"<td class='name-col'>{row.PersonName}</td>");
                sb.Append($"<td class='proj-col'>{row.ProjectName}</td>");

                for (int w = 1; w <= 52; w++)
                {
                    bool hasWork = row.ActiveWeeks.Contains(w);
                    string cellClass = hasWork ? "class='work'" : "";
                    string cellValue = hasWork ? "40" : ""; 
                    sb.Append($"<td {cellClass}>{cellValue}</td>");
                }
                sb.Append("</tr>");
            }

            sb.Append("</table></body></html>");

            // 5. write to file
            File.WriteAllText(exportPath, sb.ToString());
            Console.WriteLine($"\n[Success] 🖍️ The sorted heatmap has been saved: {exportPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Export failed: " + ex.Message);
        }
    }


    //checking in GreedyAlg now
    // private void GreedyChecker(string label, ScheduleState state)
    // {
    //     int totalAssignments = state.PersonWeekGrid.Values.Sum();
    //     int nonConflictAssignments = state.PersonWeekGrid
    //         .Where(kv => kv.Value == 1)
    //         .Sum(kv => kv.Value);
    //     int doubleBooked = state.PersonWeekGrid.Count(kv => kv.Value >= 2);

    //     double pctNotDoubleBooked = totalAssignments == 0
    //         ? 100
    //         : (double)nonConflictAssignments / totalAssignments * 100;

    //     Console.WriteLine(label + " Total assignments=" + totalAssignments
    //         + " Double-booked weeks=" + doubleBooked
    //         + " % not double-booked=" + pctNotDoubleBooked.ToString("0.##"));
    // }

    public void testPrint(ScheduleState state)
    {
        foreach (var p in state.People)
        {
            Console.WriteLine("Name: " + p.id + " | Role: " + p.role);
        }
    }
    public void testPrint(List<Person> people)
    {
        // Console.WriteLine("People:");
        foreach (var p in people)
        {
        //     Console.WriteLine("- " + p.name);
        //     foreach (KeyValuePair<Project, List<int>> kvp in p.projects)
        //     {
        //         List<int> values = kvp.Value;
        //         foreach (var v in values)
        //         {
        //             Console.WriteLine("Key = {0}, Value = {1}", kvp.Key.name, v);
        //         }
        //     }
            Console.WriteLine("Name: " + p.id + " | Role: " + p.role);
        }
        // Console.WriteLine("Count of projects: " + projects.Count);
    }

    // public void test_ConflictClass()
    // {
    //     var conflict = new Conflict
    //     {
    //         PersonId = 1,
    //         PersonName = "Person_01",
    //         Week = 15,
    //         ProjectCount = 2,
    //         ProjectNames = new List<string> { "Project_001", "Project_002" }
    //     };
    //     Console.WriteLine($"{conflict.PersonName} has {conflict.ProjectCount} projects in week {conflict.Week}");
    //     Console.WriteLine($"Projects: {string.Join(", ", conflict.ProjectNames)}");
    // }

    // private void test_Report()
    // {
    //     var report = new ConflictReport();

    //     report.Conflicts.Add(new Conflict
    //     {
    //         PersonName = "Person_01",
    //         Week = 15,
    //         ProjectCount = 2,
    //         ProjectNames = new List<string> { "Project_001", "Project_002" }
    //     });

    //     report.Conflicts.Add(new Conflict
    //     {
    //         PersonName = "Person_02",
    //         Week = 20,
    //         ProjectCount = 3,
    //         ProjectNames = new List<string> { "Project_003", "Project_004", "Project_005" }
    //     });

    //     report.PrintReport();
    // }

    // private void test_SimpleDetector()
    // {

    //     var detector = new ConflictDetector();

    //     // Create temporary grid data for testing
    //     var testGrid = new Dictionary<(int, int), int>
    //     {
    //         { (1, 15), 2 },  // Person 1, Week 15, 2 projects (conflict)
    //         { (1, 16), 1 },  // Person 1, Week 16, 1 project (no conflict)
    //         { (2, 20), 3 },  // Person 2, Week 20, 3 projects (conflict)
    //         { (3, 25), 1 },  // Person 3, Week 25, 1 project (no conflict)
    //     };

    //     var report = detector.DetectConflictsSimple(testGrid);
    //     report.PrintReport();

    //     Console.WriteLine($"Expected 2 conflicts, got {report.Conflicts.Count}");
    // }


    public void testAlgo(ScheduleState state)
    {
        // think this is double booking count, taken from Greedy
        // Console.WriteLine("Writing");
    }

    static void Main(string[] args)
    {
        new Program();
    }

}

    public class AssignmentRow
{
    public string PersonName { get; set; }
    public string ProjectName { get; set; }
    public int StartWeek { get; set; }
    public HashSet<int> ActiveWeeks { get; set; }
    public int Duration { get; set; }
    public int PeopleCount { get; set; }
}
