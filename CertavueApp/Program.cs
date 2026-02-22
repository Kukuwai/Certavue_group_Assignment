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



    // public Program()
    public async Task RunAsync() // I changed it to run with Ollama because C# doesn't allow async constructors so we make it a regular async method.
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
        string[] files = Directory.GetFiles(dataDirectory, "*.csv");
        var outputCsvDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "Outputcsv"));
        Directory.CreateDirectory(outputCsvDir);


        ScheduleState finalState = null;
        string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        OpenAI openAI = new OpenAI(apiKey, "gpt-5.2");


        // loading data in
        foreach (string file in files)
        {
            if (!file.Contains("realistic_min10_xxlarge_23projects_20people_sorted"))
            {
                continue;
            }
            var originalState = loadData(file);
            ScheduleHandler originalHandler = new ScheduleHandler(originalState);
            // Console.WriteLine("\n>>> [Before Optimization] Orignal conflicts detai:");
            // originalHandler.DebugConflictDetails(originalState);

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
            string baseName = Path.GetFileNameWithoutExtension(file);
            string outputPath = Path.Combine(outputCsvDir, baseName + "_after_greedy.csv");
            ScheduleHandler afterHandler = new ScheduleHandler(scheduleAfterGreedy);
            // Console.WriteLine("\n>>> [After Greedy] Conflictes detais:");
            // afterHandler.DebugConflictDetails(scheduleAfterGreedy);
            ScheduleCsvExporter.ExportStateToWeeklyTableCsv(scheduleAfterGreedy, outputPath);
            //string instructionsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "Instructions.txt"));

            //-----------------------⚠️this part is adding to run ortools---------------
            Console.WriteLine($"\nOR-Tools Running File - {System.IO.Path.GetFileName(file)}");
            var stateOrTools = loadData(file); // Re-load original data to ensure the optimizer starts from a clean baseline
            // Backup original assignments to calculate movement costs and map solver results back to business objects
            var backupOrTools = stateOrTools.Projects.ToDictionary(p => p.id, p => stateOrTools.GetOriginalAssignments(p));
            
            var optimizer = new CpSatOptimizer();
            var orToolsResult = optimizer.Optimize(stateOrTools, backupOrTools, maxSeconds: 60.0);

            string orToolsOutputPath = Path.Combine(outputCsvDir, baseName + "_after_ortools.csv");
            if (orToolsResult.Status == CpSolverStatus.Feasible || orToolsResult.Status == CpSolverStatus.Optimal)
            {
                // Map solver variables back to the ScheduleState model
               stateOrTools.UpdateFromFineGrainedAssignments(orToolsResult.Assignments, backupOrTools);
                ScheduleCsvExporter.ExportStateToWeeklyTableCsv(stateOrTools, orToolsOutputPath);

                printStats("OR-Tools Optimization", stateOrTools, file, true);
            }
         
            // I am using a smaller Instruction file with only essential questions as Ollama can not handle large prompts.
        //     string instructionsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "Instructions_ollama.txt"));
            // ⚠️Those lines which has double "//" is new adding.
        //     // string responseText = openAI.CompareTwoCsvWithInstructions(file, outputPath, instructionsPath);

        //     string documentsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents"));
        //     Directory.CreateDirectory(documentsDir);

        //     // ⚠️Perform a three-way analysis: Original Baseline vs. Greedy (Quick Fix) vs. OR-Tools (Global Optimal)
        //     // This allows the LLM to synthesize a final strategic plan rather than just comparing files.
        //     string integratedStrategy = openAI.AnalyzeThreeWayStrategy(
        //            file,                // Raw status (Input)
        //            greedyOutputPath,    
        //            orToolsOutputPath,   
        //            instructionsPath     
        //     );

        //   // Export the final AI-generated strategic report
        //   File.WriteAllText(Path.Combine(documentsDir, baseName + "_Final_Strategy_Report.txt"), integratedStrategy);

            // string responsePath = Path.Combine(documentsDir, baseName + "_OpenAI_Response.txt");
            // Console.WriteLine("Wrote CSV: " + outputPath);
            // //File.WriteAllText(responsePath, responseText);
            // Console.WriteLine("Saved OpenAI response: " + responsePath);

            // // ******************** OLLAMA TEST **************************
            // Console.WriteLine("\nTesting Ollama for comparison...");
            // OllamaScheduleExplainer ollamaExplainer = new OllamaScheduleExplainer("llama3.2:3b");

            // string ollamaResponse = await ollamaExplainer.CompareTwoCsvWithInstructions(
            //     file,
            //     outputPath,
            //     instructionsPath
            // );

            // string ollamaResponsePath = Path.Combine(documentsDir, baseName + "_Ollama_Response.txt");
            // File.WriteAllText(ollamaResponsePath, ollamaResponse); // here actual response is being written in the response.txt file. 
            // Console.WriteLine("Saved Ollama response: " + ollamaResponsePath);
            // ollamaExplainer.Close();
            // =================================

            // output.ExportToHtml(file, scheduleAfterGreedy, "after_greedy");

            // string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            // OpenAI openAI = new OpenAI(apiKey, "gpt-5-mini");

            // Console.WriteLine("Model: " + openAI.GetModel());
            // Console.WriteLine("Connected: " + openAI.IsConnected());

            // openAI.Close();



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
        // openAI.Close();



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

    // static void Main(string[] args)
    static async Task Main(string[] args)
    {
        await new Program().RunAsync();
        // new Program();
        // string apiKey = Environment.GetEnvironmentVariable("");
        // OpenAI openAI = new OpenAI("", "gpt-5.2-pro");

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
