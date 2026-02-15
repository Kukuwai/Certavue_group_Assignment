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
        string[] files = Directory.GetFiles(dataDirectory, "*.csv");

        ScheduleState finalState = null;

        // loading data in
        foreach (string file in files)
        {
            var originalState = loadData(file);

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
            output.ExportToHtml(file, scheduleAfterGreedy, "after_greedy");


            // var roleOpt = new RoleOptimizer();
            // var roleResult = roleOpt.Optimize(scheduleAfterGreedy, maxPasses: 1000);
            // Program.LatestState = roleResult.BestState;
            // output.ExportToHtml(file, roleResult.BestState, "After Role Checks");
            // printStats("Role optimiser Data", roleResult.BestState, file, true);
            // finalState = roleResult.BestState;



            // projects[0].printPeopleOnProject();
            // Console.WriteLine("-------");
            // people[0].printProjectsForPerson();

            // Console.WriteLine("Find project by person test");
            // foreach (Project p in projects)
            // {
            //     p.printPeopleOnProject();
            // }
        }

      //  ProcessNewProjectInsertion(finalState);
    }


    private void ProcessNewProjectInsertion(ScheduleState currentState)
    {
        if (currentState == null)
        {
            Console.WriteLine("[Error] No available global optimization state was found, and a new project could not be inserted.");
            return;
        }

        var newProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AddNewProject"));
        Console.WriteLine($"\n[ACTION] is searching new project: {newProjectDir}");

        if (!Directory.Exists(newProjectDir))
        {
            Console.WriteLine("[Error] can not find AddNewProject folder。");
            return;
        }

        ScheduleHandler handler = new ScheduleHandler(currentState);
        string[] newFiles = Directory.GetFiles(newProjectDir, "*.csv");

        foreach (var file in newFiles)
        {
            Console.WriteLine($"[File] is processing: {Path.GetFileName(file)}");

            List<Project> newProjects = LoadNewProjectsOnly(file);

            foreach (var project in newProjects)
            {
                currentState.AddProject(project);           //fixed these 2 lines
                double scoreDelta = handler.EvaluateNewProjectInsertion(project);  //fixed these 2 lines


                if (scoreDelta >= 0)
                {
                    Console.WriteLine($"   ✅ [Success] project '{project.name}' had insert sucessful。score change: {scoreDelta:F4}");
                }
                else
                {
                    Console.WriteLine($"   ⚠️ [Warning] project '{project.name}' after insert,score change: ({scoreDelta:F4})，please check conflicts。");
                }
            }
            Output finalOutput = new Output();
            finalOutput.ExportToHtml("Global_Final_Schedule", currentState, "With_New_Projects.html");
        }

        Console.WriteLine("[SYSTEM] The final shift schedule has been exported to an HTML file.");
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
