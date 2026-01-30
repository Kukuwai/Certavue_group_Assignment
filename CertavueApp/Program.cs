﻿using static Project;
using static Person;
using static Loader;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
public class Program
{
    List<Project> projects = new List<Project>();
    List<Person> people = new List<Person>();

    public Program()
    {
        loadData();
        //testPrint();
        var beforeGreedy = new ScheduleState(people, projects);
       // GreedyChecker("Before Greedy", beforeGreedy);
        new GreedyAlg().StartGreedy(people, projects);
        var afterGreedy = new ScheduleState(people, projects);
      //  GreedyChecker("After Greedy", afterGreedy);
    }

    public void loadData()
    {
        Loader load = new Loader();
        (this.people, this.projects) = load.LoadData("Data/schedule_target75_large.csv");
        Console.WriteLine("Loaded.");
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

    public void testPrint()
    {
        // Console.WriteLine("People:");
        // foreach (var p in people)
        // {
        //     Console.WriteLine("- " + p.name);
        //     foreach (KeyValuePair<Project, List<int>> kvp in p.projects)
        //     {
        //         List<int> values = kvp.Value;
        //         foreach (var v in values)
        //         {
        //             Console.WriteLine("Key = {0}, Value = {1}", kvp.Key.name, v);
        //         }
        //     }

        // }
        // Console.WriteLine("Count of projects: " + projects.Count);
    }

    public void test_ConflictClass()
    {
        var conflict = new Conflict
        {
            PersonId = 1,
            PersonName = "Person_01",
            Week = 15,
            ProjectCount = 2,
            ProjectNames = new List<string> { "Project_001", "Project_002" }
        };
        Console.WriteLine($"{conflict.PersonName} has {conflict.ProjectCount} projects in week {conflict.Week}");
        Console.WriteLine($"Projects: {string.Join(", ", conflict.ProjectNames)}");
    }

    private void test_Report()
    {
        var report = new ConflictReport();

        report.Conflicts.Add(new Conflict
        {
            PersonName = "Person_01",
            Week = 15,
            ProjectCount = 2,
            ProjectNames = new List<string> { "Project_001", "Project_002" }
        });

        report.Conflicts.Add(new Conflict
        {
            PersonName = "Person_02",
            Week = 20,
            ProjectCount = 3,
            ProjectNames = new List<string> { "Project_003", "Project_004", "Project_005" }
        });

        report.PrintReport();
    }

    private void test_SimpleDetector()
    {

        var detector = new ConflictDetector();

        // Create temporary grid data for testing
        var testGrid = new Dictionary<(int, int), int>
        {
            { (1, 15), 2 },  // Person 1, Week 15, 2 projects (conflict)
            { (1, 16), 1 },  // Person 1, Week 16, 1 project (no conflict)
            { (2, 20), 3 },  // Person 2, Week 20, 3 projects (conflict)
            { (3, 25), 1 },  // Person 3, Week 25, 1 project (no conflict)
        };

        var report = detector.DetectConflictsSimple(testGrid);
        report.PrintReport();

        Console.WriteLine($"Expected 2 conflicts, got {report.Conflicts.Count}");
    }

    static void Main(string[] args)
    {
        new Program();
    }
}
