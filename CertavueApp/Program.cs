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
    private readonly string dataPath;



    public Program()
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
        string[] files = Directory.GetFiles(dataDirectory, "*.csv");
        // loading data in
        foreach (string file in files)
        {
            dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule_target75_medium_with_roles_40s.csv");
            var originalState = loadData(dataPath);

            // export original data to html output
            Output output = new Output();
            output.ExportToHtml(dataPath, originalState, "Original");
            printStats("Original Data", originalState);

            // moveByConflict method (manual optimisation)
            var scheduleAfterConflict = new MoveByConflict().start(originalState, projects);
            output.ExportToHtml(dataPath, scheduleAfterConflict, "after_conflict");
            printStats("Conflict Moving Data", scheduleAfterConflict);

            // greedy algorithm starts, inluding export of output to html
            var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
            output.ExportToHtml(dataPath, scheduleAfterGreedy, "after_greedy");

            //testPrint(scheduleAfterGreedy);
            //testAlgo(scheduleAfterGreedy, "After Greedy");
            var roleOpt = new RoleOptimizer();
            var roleResult = roleOpt.Optimize(scheduleAfterGreedy, maxPasses: 999999999);

            //testPrint(scheduleAfterGreedy);
            //testAlgo(scheduleAfterGreedy, "After_RoleOptimizer");

            Output output2 = new Output();
            output2.ExportToHtml(dataPath, originalState, "After Role Checks");
        }
    }
    public ScheduleState loadData(string path)
    {
        Loader load = new Loader();
        (var people, var projects) = load.LoadData(path);
        var state = new ScheduleState(people, projects);
        this.people = people;
        this.projects = projects;
        Console.WriteLine("Loaded.\n");
        return state;
    }


    static void Main(string[] args)
    {
        new Program();
    }

    public void printStats(string dataName, ScheduleState state)
    {
        ScheduleHandler handler = new ScheduleHandler(state);
        var conflictScore = handler.GetConflictScore(state);
        var movementScore = handler.GetMovementScore(state);
        var focusScore = handler.GetFocusScore(state);
        var continuityScore = handler.GetContinuityScore(state);
        var durationScore = handler.GetDurationScore(state);
        var fitnessScore = handler.CalculateFitnessScore(state);
        Console.WriteLine($"|-----{dataName}-----|");
        Console.WriteLine($"Finess Score - {fitnessScore}\nBreakdown - Conflict Score: {conflictScore} || Movement Score: {movementScore} || Focus Score: {focusScore} || Continuity Score: {continuityScore} || Duration Score: {durationScore}\n");
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
    public string PersonRole { get; internal set; }
}
