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

    public static ScheduleState LatestState;



    public Program()
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
       //string[] files = Directory.GetFiles(dataDirectory, "*.csv");
       //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_spectacular_fitness_mixedD_varied40s.csv") };
       //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_spectacular_fitness_mixedC_varied40s.csv") };
       //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_spectacular_fitness_mixedB_varied40s.csv") };
        //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_spectacular_fitness_mixedA_varied40s.csv") };
      // string[] files = new string[] { Path.Combine(dataDirectory, "schedule_role_optimizer_hits_100_after_greedy_stuck_varied40s.csv") };
       string[] files = new string[] { Path.Combine(dataDirectory, "schedule_requires_role_optimizer_greedy_stuck_B_varied40s.csv") };
       //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_requires_role_optimizer_greedy_stuck_A_varied40s.csv") };
       //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_project_contiguous_fitness_medium_improvable_varied40s.csv") };
       //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_project_contiguous_fitness_low_improvable_varied40s.csv") };
       //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_project_contiguous_fitness_high_improvable_varied40s.csv") };
       //string[] files = new string[] { Path.Combine(dataDirectory, "schedule_project_contiguous_fitness_extreme_improvable_varied40s.csv") };
      
        ScheduleState finalState = null;
        

        // loading data in
        foreach (string file in files)
        {
            var originalState = loadData(file);
            ScheduleHandler handler = new ScheduleHandler(originalState);
            Console.WriteLine(">>> initial state analye:");
            handler.DebugConflictDetails(originalState);
            // Console.WriteLine("\n--- PersonWeekGrid checking vaule ---");
            // int count = 0;
            // foreach (var entry in originalState.PersonWeekGrid)
            // {
            //   if (count++ >= 10) break; 
    
            //    Console.WriteLine($"Key content: {entry.Key} | (Value): {entry.Value}");
            //  }
            // Console.WriteLine("----------------------------------\n");



            // export original data to html output
            Output output = new Output();
            output.ExportToHtml(file, originalState, "Original");
            printStats("Original Data", originalState, file, false);

            // moveByConflict method (manual optimisation)
            // var scheduleAfterConflict = new MoveByConflict().start(originalState, projects);
            // output.ExportToHtml(file, scheduleAfterConflict, "after_conflict");
            // printStats("Conflict Moving Data", scheduleAfterConflict, file);

            /*Console.WriteLine("start move conflict");
            var scheduleAfterConflict = new MoveByConflict().start(originalState, projects);
            output.ExportToHtml(file, scheduleAfterConflict, "after_conflict");
            printStats("Conflict Moving Data", scheduleAfterConflict, file);*/


            // greedy algorithm starts, inluding export of output to html
            Console.WriteLine($"Greeding Running File - {System.IO.Path.GetFileName(file)}\n");
            var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
            Console.WriteLine(">>> After Greedy conflicts detail:");
            handler.DebugConflictDetails(scheduleAfterGreedy);
            output.ExportToHtml(file, scheduleAfterGreedy, "after_greedy");


            var roleOpt = new RoleOptimizer();
            var roleResult = roleOpt.Optimize(scheduleAfterGreedy, maxPasses: 999999999);
            Program.LatestState = roleResult.BestState;// * add newest state
            Console.WriteLine(">>> Role Optimizer final conflicts detail:");
            handler.DebugConflictDetails(roleResult.BestState);
            output.ExportToHtml(file, scheduleAfterGreedy, "After Role Checks");
            printStats("Role optimiser Data", roleResult.BestState, file, true);


            projects[0].printPeopleOnProject();
            Console.WriteLine("-------");
            people[0].printProjectsForPerson();

            finalState = roleResult.BestState;


            // Console.WriteLine("Find project by person test");
            // foreach (Project p in projects)
            // {
            //     p.printPeopleOnProject();
            // }
             }

     ProcessNewProjectInsertion(finalState);
    }
    

private void ProcessNewProjectInsertion(ScheduleState currentState)
{

    var newProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AddNewProject"));
    if (!Directory.Exists(newProjectDir))
    {
        Console.WriteLine("[Error] can not find AddNewProject folder.");
        return;
    }

    ScheduleHandler handler = new ScheduleHandler(currentState);
    string[] newFiles = Directory.GetFiles(newProjectDir, "*.csv");

    foreach (var file in newFiles)
    {
        Console.WriteLine($"\n[File] is processing: {Path.GetFileName(file)}");
        List<Project> newProjects = LoadNewProjectsOnly(file);

        foreach (var project in newProjects)
        {
            double insertionResult = handler.EvaluateNewProjectInsertion(project);

            if (insertionResult >= 1.0)
            {

                Console.WriteLine($"   ✅ [INSERT SUCCESSFUL] Project '{project.name}' find avalible time，no extrac conflicts。");
            }
            else
            {
                int conflictCount = (int)Math.Abs(insertionResult);
                Console.WriteLine($"   ⚠️ [FORCE INSERT] Project '{project.name}' can not avoid conflicts，lead to add new {conflictCount} conflicts。");
            }
        }
        
        Output finalOutput = new Output();
        finalOutput.ExportToHtml("Global_Final_Schedule", currentState, "With_New_Projects.html");
    }

    Console.WriteLine("\n[SYSTEM] finish insert，final output alreay generate。");
}

    public List<Project> LoadNewProjectsOnly(string path)
    {
        Loader load = new Loader();
        (_, var newProjects) = load.LoadData(path);
        return newProjects;
    }

    public ScheduleState loadData(string path)
    {
        Loader load = new Loader();
        (var people, var projects) = load.LoadData(path);
        var state = new ScheduleState(people, projects);
        this.people = people;
        this.projects = projects;
        Console.WriteLine($"Loaded {System.IO.Path.GetFileName(path)}\n");
        return state;
    }

    static void Main(string[] args)
    {
        new Program();
    }

    public void printStats(string dataName, ScheduleState state, string path, bool end)
    {
        ScheduleHandler handler = new ScheduleHandler(state);
        var conflictScore = handler.GetConflictScore(state);
        var movementScore = handler.GetMovementScore(state);
        var focusScore = handler.GetFocusScore(state);
        var continuityScore = handler.GetContinuityScore(state);
        var durationScore = handler.GetDurationScore(state);
        var fitnessScore = handler.CalculateFitnessScore(state);
        Console.WriteLine($"|-----{dataName}-----|");
        Console.WriteLine($"Finess Score - {fitnessScore.ToString("F2")}\nBreakdown - Conflict Score: {conflictScore.ToString("F2")} || Movement Score: {movementScore.ToString("F2")} || Focus Score: {focusScore.ToString("F2")} || Continuity Score: {continuityScore.ToString("F2")} || Duration Score: {durationScore.ToString("F2")}\n");
        if (end)
        {
            Console.WriteLine("------------------------------------------------------------------\n");
        }
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
