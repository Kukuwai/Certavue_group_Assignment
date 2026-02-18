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


        ScheduleState finalState = null;
        // string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        // OpenAI openAI = new OpenAI(apiKey, "gpt-5-nano");


        // loading data in
        foreach (string file in files)
        {
            if (!file.Contains("realistic_min10_small_12projects_9people_sorted.csv"))
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
            // string instructionsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents", "Instructions.txt"));

            // string responseText = openAI.CompareTwoCsvWithInstructions(file, outputPath, instructionsPath);

            // string documentsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Documents"));
            // Directory.CreateDirectory(documentsDir);

            // string responsePath = Path.Combine(documentsDir, baseName + "_OpenAI_Response.txt");
            // Console.WriteLine("Wrote CSV: " + outputPath);
            // File.WriteAllText(responsePath, responseText);
            // Console.WriteLine("Saved OpenAI response: " + responsePath);



            output.ExportToHtml(file, scheduleAfterGreedy, "after_greedy");
            // Here the method FindPeopleForNewProject method is tested with both original Schedule and the Greedy Optimized Schedule.
            TestFindPeopleForNewProject(originalState, "Original Schedule");
            TestFindPeopleForNewProject(scheduleAfterGreedy, "After Greedy Schedule");

            TestFindWorkForNewPerson(originalState, "Original Schedule");
            TestFindWorkForNewPerson(scheduleAfterGreedy, "After Greedy Schedule");

            TestCountOverloadedWeeks(originalState, "Original Schedule");
            TestCountOverloadedWeeks(scheduleAfterGreedy, "After Greedy Schedule");

            TestFindLeastBusyWeeks(originalState, "Original Schedule");
            TestFindLeastBusyWeeks(scheduleAfterGreedy, "After Greedy Schedule");

            TestGetAvailableWeeksForPerson(originalState, "Original Schedule");
            TestGetAvailableWeeksForPerson(scheduleAfterGreedy, "After Greedy Schedule");

            TestGetAvailablePeopleInWeek(originalState, "Original Schedule");
            TestGetAvailablePeopleInWeek(scheduleAfterGreedy, "After Greedy Schedule");

            TestGetPersonWorkload(originalState, "Original Schedule");
            TestGetPersonWorkload(scheduleAfterGreedy, "After Greedy Schedule");

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

    // This test method takes state as the argument and then it passes it to the constructor of AvailabiltyFinder. 
    // Here I use label to know the state whether it is before Greedy or After Greedy. 
    public void TestFindPeopleForNewProject(ScheduleState state, string label)
    {
        Console.WriteLine($"\n*** Availability Finder - {label} ***\n");
        var finder = new AvailabilityFinder(state);

        var result = finder.FindPeopleForNewProject(
            startWeek: 25,
            duration: 5,
            peopleNeeded: 4,
            requiredRole: "developer"
        );

        result.PrintSummary();
    }
    // This Test method runs FindWorkForNewPerson method and takes state and corresponding label as arguments. 
    public void TestFindWorkForNewPerson(ScheduleState state, string label)
    {
        Console.WriteLine($"\n*** Availability Finder - {label} ***");
        var finder = new AvailabilityFinder(state);

        var result = finder.FindWorkForNewPerson(
            personName: "NewPerson",
            availableWeeks: 52

        );

        result.PrintSummary();
    }

    public void TestCountOverloadedWeeks(ScheduleState state, string label)
    {
        Console.WriteLine($"\n*** Count Overloaded Weeks - {label} ***");
        var finder = new AvailabilityFinder(state);

        // Test for first few people in the dataset
        foreach (var person in state.People.Take(5))
        {
            int overloadedWeeks = finder.CountOverloadedWeeks(person.name);
            Console.WriteLine($"{person.name} ({person.role}): {overloadedWeeks} overloaded weeks (capacity: {person.capacity}h/week)");
        }
        Console.WriteLine("***************************\n");
    }

    public void TestFindLeastBusyWeeks(ScheduleState state, string label)
    {
        Console.WriteLine($"\n*** Find Least Busy Weeks - {label} ***");
        var finder = new AvailabilityFinder(state);

        var leastBusyWeeks = finder.FindLeastBusyWeeks(numberOfWeeks: 5);

        Console.WriteLine("Top 5 least busy weeks:");
        foreach (var week in leastBusyWeeks)
        {
            int totalHours = state.PersonWeekGrid
                .Where(kvp => kvp.Key.Week == week)
                .Sum(kvp => kvp.Value);

            Console.WriteLine($"  Week {week}: {totalHours} total hours allocated");
        }
        Console.WriteLine("***************************\n");
    }

    public void TestGetAvailableWeeksForPerson(ScheduleState state, string label)
    {
        Console.WriteLine($"\n*** Get Available Weeks - {label} ***");
        var finder = new AvailabilityFinder(state);

        // Test for first few people in the dataset
        foreach (var person in state.People.Take(5))
        {
            var availableWeeks = finder.GetAvailableWeeksForPerson(person.name);

            if (availableWeeks.Count > 0)
            {
                Console.WriteLine($"{person.name} ({person.role}): {availableWeeks.Count} free weeks");
                Console.WriteLine($"  Weeks: {string.Join(", ", availableWeeks.Take(10))}{(availableWeeks.Count > 10 ? "..." : "")}");
            }
            else
            {
                Console.WriteLine($"{person.name} ({person.role}): No free weeks (fully booked)");
            }
        }
        Console.WriteLine("***************************\n");
    }

    public void TestGetAvailablePeopleInWeek(ScheduleState state, string label)
    {
        Console.WriteLine($"\n*** Get Available People In Week - {label} ***");
        var finder = new AvailabilityFinder(state);

        // Test a few different weeks
        int[] weeksToCheck = { 1, 10, 20, 25, 30 };

        foreach (var week in weeksToCheck)
        {
            var availablePeople = finder.GetAvailablePeopleInWeek(week);

            Console.WriteLine($"Week {week}: {availablePeople.Count} people available");
            if (availablePeople.Count > 0)
            {
                Console.WriteLine($"  Available: {string.Join(", ", availablePeople.Select(p => $"{p.name} ({p.role})"))}");

            }
            else
            {
                Console.WriteLine($"  All people are busy this week");
            }
        }
        Console.WriteLine("***************************\n");
    }

    public void TestGetPersonWorkload(ScheduleState state, string label)
    {
        Console.WriteLine($"\n*** Get Person Workload - {label} ***");
        var finder = new AvailabilityFinder(state);

        // Test first few people across several weeks
        foreach (var person in state.People.Take(3))
        {
            Console.WriteLine($"\n{person.name} ({person.role}) - Capacity: {person.capacity}h/week:");

            int[] weeksToCheck = { 10, 15, 20, 25, 30 };
            foreach (var week in weeksToCheck)
            {
                int workload = finder.GetPersonWorkload(person.name, week);
                string status = workload == 0 ? "Free" :
                               workload >= person.capacity ? "OVERLOADED" :
                               "Available";

                Console.WriteLine($"  Week {week}: {workload}h allocated - {status}");
            }
        }
        Console.WriteLine("***************************\n");
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
            //Console.WriteLine($"[File] is processing: {Path.GetFileName(file)}");

            List<Project> newProjects = LoadNewProjectsOnly(file);

            foreach (var project in newProjects)
            {
                currentState.AddProject(project);           //fixed these 2 lines
                double scoreDelta = handler.EvaluateNewProjectInsertion(project);  //fixed these 2 lines


                if (scoreDelta >= 0)
                {
                    //Console.WriteLine($"   ✅ [Success] project '{project.name}' had insert sucessful。score change: {scoreDelta:F4}");
                }
                else
                {
                    //Console.WriteLine($"   ⚠️ [Warning] project '{project.name}' after insert,score change: ({scoreDelta:F4})，please check conflicts。");
                }
            }
            Output finalOutput = new Output();
            finalOutput.ExportToHtml("Global_Final_Schedule", currentState, "With_New_Projects.html");
        }

        //Console.WriteLine("[SYSTEM] The final shift schedule has been exported to an HTML file.");
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

    public void TestFindPeopleForNewProject()
    {
        Console.WriteLine("\n********Testing FindPeopleForNewProject method****** \n");

        // ===== CHECK DATA BEFORE SCHEDULESTATE ===== 
        Console.WriteLine("=== CHECKING Person_08 RAW DATA ===");
        var person08 = people.FirstOrDefault(p => p.name == "Person_08");

        if (person08 != null)
        {
            Console.WriteLine($"Person_08 has {person08.projects.Count} projects in raw data");

            // Check first project
            var firstProject = person08.projects.FirstOrDefault();
            if (firstProject.Key != null)
            {
                Console.WriteLine($"\nFirst project: {firstProject.Key.name}");
                Console.WriteLine($"Weeks for this project: {firstProject.Value.Count}");

                // Show some week entries
                foreach (var weekEntry in firstProject.Value.Take(5))
                {
                    Console.WriteLine($"  Week {weekEntry.Key}: {weekEntry.Value} hours");
                }
            }
        }
        Console.WriteLine("=========================\n");
        // ===== END CHECK =====

        var state = new ScheduleState(people, projects);

        var finder = new AvailabilityFinder(state);

        var result = finder.FindPeopleForNewProject(
            startWeek: 25,
            duration: 5,
            peopleNeeded: 4,
            requiredRole: "developer"
        );

        result.PrintSummary();
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
