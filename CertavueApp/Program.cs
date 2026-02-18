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
using static OpenAI;
using Google.OrTools.Sat;


public class Program
{
    List<Project> projects = new List<Project>();
    List<Person> people = new List<Person>();

    public static ScheduleState LatestState;



    public Program()
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
        string[] files = Directory.GetFiles(dataDirectory, "*.csv");
        var outputCsvDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "Outputcsv"));
        Directory.CreateDirectory(outputCsvDir);


        //ScheduleState finalState = null;
        string apiKey = Environment.GetEnvironmentVariable("ApiKey");
        OpenAI openAI = new OpenAI(apiKey, "gpt-5-mini");


        // loading data in
        foreach (string file in files)
        {
            if (!file.Contains("schedule_project_contiguous_fitness_high_improvable_varied40s.csv"))
            {
                continue;
            }
            var originalState = loadData(file);//first time hold state!!!!
            var backup = originalState.Projects.ToDictionary(
            p => p.id, 
            p => originalState.GetOriginalAssignments(p)
            );

            //ScheduleHandler originalHandler = new ScheduleHandler(originalState);
            Console.WriteLine("\n>>> [Before Optimization] Orignal conflicts detai:");
            //originalHandler.DebugConflictDetails(originalState);
            printStats(file, originalState, "Original", false);
            ScheduleCsvExporter.ExportStateToWeeklyTableCsv(originalState, outputCsvDir + "/outputOriginal.csv");
            foreach (Project p in projects)
            {
                int h = p.getTotalHours();
                Console.WriteLine($"Project: {p.id} | Hours: {h}");
            }

            // export original data to html output
            // Output output = new Output();
            // output.ExportToHtml(file, originalState, "Original");
            // printStats("Original Data", originalState, file, false);

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
            var greedyState = new GreedyAlg().StartGreedy(originalState.People, originalState.Projects);//!!!second time hold state!!!
            string baseName = Path.GetFileNameWithoutExtension(file);
            string outputPath = Path.Combine(outputCsvDir, baseName + "_after_greedy.csv");
            ScheduleHandler afterHandler = new ScheduleHandler(greedyState);
            Console.WriteLine("\n>>> [After Greedy] Conflictes detais:");
            afterHandler.DebugConflictDetails(greedyState);
            printStats(file, greedyState, "Greedy", false);
            ScheduleCsvExporter.ExportStateToWeeklyTableCsv(greedyState, outputCsvDir + "/outputGreedy.csv");
            foreach (Project p in greedyState.Projects)
            {
                int h = p.getTotalHours();
                Console.WriteLine($"Project: {p.id} | Hours: {h}");
            }

           // run or tools
            Console.WriteLine("\n>>> [3. After OR-Tools] Optimization Starting...");
            var optimizer = new CpSatOptimizer();

           // We pass 'originalState' for constraints and 'backup' (the raw data) to ensure 
           // the solver accounts for every single hour, avoiding data loss from previous steps.
            var result = optimizer.Optimize(originalState, backup, 60); 
            Console.WriteLine($"\n>>> [After Ortools] Solver Status: {result.Status}");

           // Proceed only if the solver found a valid (Feasible) or the best (Optimal) solution.
            if (result.Status == Google.OrTools.Sat.CpSolverStatus.Feasible || result.Status == Google.OrTools.Sat.CpSolverStatus.Optimal)
        {
             // UPDATE LOGIC: Map the solver's mathematical results back to our domain objects.
             // This method reassigns hours and calls RebuildGrid() to refresh the global load map.
            originalState.UpdateFromFineGrainedAssignments(result.Assignments, backup);
    
            var finalHandler = new ScheduleHandler(originalState);
            finalHandler.DebugConflictDetails(originalState);
    
             printStats(file, originalState, "CPSAT_Only", true);
    
            Console.WriteLine("--- Optional strategy ---");
            Console.WriteLine($"Successful Reduction: {result.Report.ConflictReduced}h");
            Console.WriteLine($"Extension Duration: {result.Report.TotalDelayWeeks}");
            Console.WriteLine($"Adding more same-role people: {result.Report.ResourceSwaps}");

           // Using 'originalState' ensures we are exporting the fully reconciled data.
           // ScheduleCsvExporter.ExportStateToWeeklyTableCsv(originalState, outputCsvDir + "/outputSolver.csv");

            foreach (Project p in originalState.Projects)
           {
            int h = p.getTotalHours();
            Console.WriteLine($"Project: {p.id} | Hours: {h}");
           }
        }

            // ScheduleCsvExporter.ExportStateToWeeklyTableCsv(scheduleAfterGreedy, outputPath);
            // string instructionsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "Instructions.txt"));

            // string responseText = openAI.CompareTwoCsvWithInstructions(file, outputPath, instructionsPath);

            // string documentsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents"));
            // Directory.CreateDirectory(documentsDir);

            // string responsePath = Path.Combine(documentsDir, baseName + "_OpenAI_Response.txt");

            // File.WriteAllText(responsePath, responseText);
            // Console.WriteLine("Saved OpenAI response: " + responsePath);

            // Console.WriteLine("Wrote CSV: " + outputPath);

            // output.ExportToHtml(file, scheduleAfterGreedy, "after_greedy");

            // string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            // OpenAI openAI = new OpenAI(apiKey, "gpt-5-mini");

            // Console.WriteLine("Model: " + openAI.GetModel());
            // Console.WriteLine("Connected: " + openAI.IsConnected());

            // openAI.Close();



            

           



            // projects[0].printPeopleOnProject();
            // Console.WriteLine("-------");
            // people[0].printProjectsForPerson();

            // Console.WriteLine("Find project by person test");
            // foreach (Project p in projects)
            // {
            //     p.printPeopleOnProject();
            // }
        }
        //openAI.Close();

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


   //a quick wrapper to push OR-Tools results into the state and refresh everything.
    // static void ApplyAssignmentsToState(ScheduleState state, Dictionary<(int PersonId, Project Project, int RawWeek), int> assignments)
    // {
    // state.UpdateFromFineGrainedAssignments(result.Assignments, backup);
    // state.RebuildGrid();
    // }

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
        // string apiKey = Environment.GetEnvironmentVariable("");
        // OpenAI openAI = new OpenAI("", "gpt-5-mini");

        // string reply = openAI.SendPrompt("What is the capital of france?");
        // Console.WriteLine(reply);

        // openAI.Close();

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
