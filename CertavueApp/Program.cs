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



    public Program(bool includeNewProject)
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
        string[] files = Directory.GetFiles(dataDirectory, "*.csv");
        // loading data in
        foreach (string file in files)
        {
            var originalState = loadData(file);
            
            // export original data to html output
            Console.WriteLine("start main progress");
            Output output = new Output();
            output.ExportToHtml(file, originalState, "Original");
            printStats("Original Data", originalState, file);

            // moveByConflict method (manual optimisation)
            Console.WriteLine("start move conflict");
            var scheduleAfterConflict = new MoveByConflict().start(originalState, projects);
            output.ExportToHtml(file, scheduleAfterConflict, "after_conflict");
            printStats("Conflict Moving Data", scheduleAfterConflict, file);

            // greedy algorithm starts, inluding export of output to html
            Console.WriteLine("start greedy");
            var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
            output.ExportToHtml(file, scheduleAfterGreedy, "after_greedy");

            //testPrint(scheduleAfterGreedy);
            //testAlgo(scheduleAfterGreedy, "After Greedy");
            Console.WriteLine("start optimal role");
            var roleOpt = new RoleOptimizer();
            var roleResult = roleOpt.Optimize(scheduleAfterGreedy, maxPasses: 1);

            //testPrint(scheduleAfterGreedy);
            //testAlgo(scheduleAfterGreedy, "After_RoleOptimizer");

            Output output2 = new Output();
            output2.ExportToHtml(file, originalState, "After Role Checks");
        }

        if (includeNewProject)
        {
            Console.WriteLine("\n[FINAL CONSOLIDATION] All files processed. Running global greedy on FULL data...");
            
            var finalOptimizedState = new GreedyAlg().StartGreedy(this.people, this.projects);
            new RoleOptimizer().Optimize(finalOptimizedState, 1);

            ProcessNewProjectInsertion(finalOptimizedState);
        }
    }


    // public Program(bool includeNewProject)
    // {
    //     var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
        
    //     if (!Directory.Exists(dataDirectory))
    //     {
    //         Console.WriteLine("Data directory not found!");
    //         return;
    //     }

    //     string[] files = Directory.GetFiles(dataDirectory, "*.csv");
    //     Console.WriteLine("Loading all baseline files...");
    //     foreach (string file in files)
    //     {
    //         loadData(file);
    //     }
    //     Console.WriteLine($"\n[SYSTEM] Total Projects: {projects.Count} | Total Staff: {people.Count}");
    //     Console.WriteLine("Starting Global Greedy Optimization on FULL data...");

    //     var finalState = new ScheduleState(this.people, this.projects);
        
    //     var finalOptimizedState = new GreedyAlg().StartGreedy(this.people, this.projects);
        
    //     Console.WriteLine($"[DEBUG] start roleOpt");
    //     var roleOpt = new RoleOptimizer();
    //     roleOpt.Optimize(finalOptimizedState, maxPasses: 99);

    //     printStats("GLOBAL BASELINE (All Data Combined)", finalOptimizedState, "All_Baseline_Files");


    //     if (includeNewProject)
    //     {
    //         Console.WriteLine("\n[ACTION] Inserting new projects into the global optimized schedule...");
    //         ProcessNewProjectInsertion(finalOptimizedState);
    //     }
    // }



    public ScheduleState loadData(string path)
   {
    Loader load = new Loader();
    (var newPeople, var newProjects) = load.LoadData(path);
    
    //* Add the newly read data to the global pool
    this.people.AddRange(newPeople);
    this.projects.AddRange(newProjects);
    var state = new ScheduleState(this.people, this.projects);
    return state;
   }



// show does new projects could be insert 
    private void ProcessNewProjectInsertion(ScheduleState currentState)
    {//make sure pass from right file path
     Console.WriteLine($"[DEBUG] start passing new file");
    var newProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AddNewProject"));
    Console.WriteLine($"[DEBUG] Searching for new projects in: {newProjectDir}");


    if (!Directory.Exists(newProjectDir)) return;

    string[] newFiles = Directory.GetFiles(newProjectDir, "*.csv");
    ScheduleHandler handler = new ScheduleHandler(currentState);

    foreach (var f in newFiles)
    {
        List<Project> newProjects = LoadNewProjectsOnly(f);
        foreach (var p in newProjects)
        {
            AddNewProjectToSystem(p, handler, currentState);
        }
    }
    
    printStats("Final Stats (With New Projects)", currentState, "New Project Insertion"); 
    }   



    public List<Project> LoadNewProjectsOnly(string path)
    {
        Loader load = new Loader();
        (_, var newProjects) = load.LoadData(path); 
        return newProjects;
    }



    public void AddNewProjectToSystem(Project incomingProject, ScheduleHandler handler, ScheduleState currentState)
    {
        double scoreDelta = handler.EvaluateNewProjectInsertion(incomingProject);
        if (scoreDelta >= 0)
        {
           Console.WriteLine($"[Success] Name: {incomingProject.name} inserted. Delta: {scoreDelta}");
        }
        else
        {
            Console.WriteLine($"[Warning] Project {incomingProject.name} insertion skipped (Delta: {scoreDelta})");
        }
    }



    static void Main(string[] args)
    {
        bool shouldAddNew = args.Contains("add-new");
        Console.WriteLine($"[DEBUG] shouldAddNew is: {shouldAddNew}");
        new Program(shouldAddNew);
    }

    public void printStats(string dataName, ScheduleState state, string path)
    {
        ScheduleHandler handler = new ScheduleHandler(state);
        var conflictScore = handler.GetConflictScore(state);
        var movementScore = handler.GetMovementScore(state);
        var focusScore = handler.GetFocusScore(state);
        var continuityScore = handler.GetContinuityScore(state);
        var durationScore = handler.GetDurationScore(state);
        var fitnessScore = handler.CalculateFitnessScore(state);
        Console.WriteLine($"|-----{dataName} : {path} -----|");
        Console.WriteLine($"Finess Score - {fitnessScore}\nBreakdown - Conflict Score: {conflictScore} || Movement Score: {movementScore} || Focus Score: {focusScore} || Continuity Score: {continuityScore} || Duration Score: {durationScore}\n");
        Console.WriteLine("------------------------------------------------------------------\n");
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
