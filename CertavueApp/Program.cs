﻿using Google.OrTools.Sat;


public class Program
{
    public static ScheduleState LatestState;
    public List<Person> people = new();
    public List<Project> projects =new ();



    public Program()
    {
        // directory building
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
      
            // greedy algorithm starts, inluding export of output to html
            Console.WriteLine($"Greeding Running File - {System.IO.Path.GetFileName(file)}\n");
            var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
            ScheduleHandler afterHandler = new ScheduleHandler(scheduleAfterGreedy);
            exportCSV(file, outputCsvDir, "_after_greedy.csv", scheduleAfterGreedy);
            

            Console.WriteLine($"\nOR-Tools Running File - {System.IO.Path.GetFileName(file)}");
            // Re-load original data to ensure the optimizer starts from a clean baseline
            var stateOrTools = loadData(file); 
            // Backup original assignments to calculate movement costs and map solver results back to business objects
            var backupOrTools = stateOrTools.Projects.ToDictionary(p => p.id, p => stateOrTools.GetOriginalAssignments(p));
            
            var optimizer = new CpSatOptimizer();
            var orToolsResult = optimizer.Optimize(stateOrTools, backupOrTools, maxSeconds: 60.0);

            if (orToolsResult.Status == CpSolverStatus.Feasible || orToolsResult.Status == CpSolverStatus.Optimal)
            {
                // Map solver variables back to the ScheduleState model
                stateOrTools.UpdateFromFineGrainedAssignments(orToolsResult.Assignments, backupOrTools);
                exportCSV(file, outputCsvDir, "_after_orTools.csv", stateOrTools);

                printStats("OR-Tools Optimization", stateOrTools, file, true);
            }
        }
        openAI.Close();
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

    public void exportCSV(string fileName, string outputPathDir, string newfileExtension, ScheduleState state)
    {
        fileName =  Path.GetFileName(fileName).Replace(".csv", newfileExtension);
        string outputPath = Path.Combine(outputPathDir, fileName);
        ScheduleCsvExporter.ExportStateToWeeklyTableCsv(state, outputPath);
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
