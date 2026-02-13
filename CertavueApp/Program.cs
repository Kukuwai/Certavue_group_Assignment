﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Program
{
    private List<Project> peopleProjects = new List<Project>();
    private List<Person> currentPeople = new List<Person>();
    public static ScheduleState LatestState;

    public static void Main(string[] args)
    {
        // 启动程序
        new Program().Run();
    }

    public void Run()
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
        if (!Directory.Exists(dataDirectory))
        {
            Console.WriteLine($"[Error] Path not found: {dataDirectory}");
            return;
        }

        string[] files = Directory.GetFiles(dataDirectory, "*.csv");

        foreach (string file in files)
        {
            // --- 1. 加载阶段 ---
            var state = loadData(file);

            // --- 2. 初始化阶段：锁定理想标尺 ---
            InitializeProjectBaselines(state);

            // --- 3. 记录原始状态 ---
            ReportState(file, state, "Original");

            // --- 4. 贪婪算法 ---
            Console.WriteLine($"Greedy Running File - {Path.GetFileName(file)}");
            var greedyAlg = new GreedyAlg();

            state = greedyAlg.StartGreedy(state.People, state.Projects);
            ReportState(file, state, "After_Greedy");

            // --- 5. 角色优化器 ---
            var roleOpt = new RoleOptimizer();
            var result = roleOpt.Optimize(state, maxPasses: 100);
            state = result.BestState;
            Program.LatestState = state;
            
            // --- 6. 最终产出 ---
            ReportState(file, state, "Final_Optimized");

            // --- 7. 后续插入逻辑 ---
            if (state != null)
            {
                ProcessNewProjectInsertion(state);
            }
        }
    }

    private void InitializeProjectBaselines(ScheduleState state)
    {
        foreach (var p in state.Projects)
        {
            // 锁定 InitialBaselineSpan
            p.InitialBaselineSpan = (p.endDate - p.startDate) + 1;

            if (p.InitialBaselineSpan <= 0) 
            {
                p.updateCapacity(); 
                p.InitialBaselineSpan = p.capacity;
            }
        }
    }

    private void ReportState(string file, ScheduleState state, string label)
    {
        Output output = new Output();
        output.ExportToHtml(file, state, label);
        printStats($"{label} Data", state, file, label != "Original");
    }

    public ScheduleState loadData(string path)
    {
        Loader load = new Loader();
        (var people, var projects) = load.LoadData(path);
        
        // 更新类成员变量供全局使用
        this.currentPeople = people;
        this.peopleProjects = projects;

        Console.WriteLine($"Loaded {Path.GetFileName(path)}");
        return new ScheduleState(people, projects);
    }

    private void ProcessNewProjectInsertion(ScheduleState currentState)
    {
        var newProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AddNewProject"));
        if (!Directory.Exists(newProjectDir)) return;

        ScheduleHandler handler = new ScheduleHandler(currentState);
        string[] newFiles = Directory.GetFiles(newProjectDir, "*.csv");

        foreach (var file in newFiles)
        {
            List<Project> newProjects = LoadNewProjectsOnly(file);
            foreach (var project in newProjects)
            {
                double result = handler.EvaluateNewProjectInsertion(project);
                if (result >= 1.0)
                    Console.WriteLine($"   ✅ [SUCCESS] {project.name}");
                else
                    Console.WriteLine($"   ⚠️ [CONFLICT] {project.name} adds {Math.Abs(result)} conflicts");
            }
            new Output().ExportToHtml("Global_Final", currentState, "With_New_Projects");
        }
    }

    public List<Project> LoadNewProjectsOnly(string path)
    {
        Loader load = new Loader();
        (_, var newProjects) = load.LoadData(path);
        return newProjects;
    }

    public void printStats(string label, ScheduleState state, string path, bool showLine)
{
    ScheduleHandler handler = new ScheduleHandler(state);
    
    // 获取 5 大维度分值
    double conflict = handler.GetConflictScore(state);
    double movement = handler.GetMovementScore(state);
    double focus = handler.GetFocusScore(state);
    double continuity = handler.GetContinuityScore(state);
    double duration = handler.GetDurationScore(state);
    double totalFitness = handler.CalculateFitnessScore(state);


    var conflicts = state.PersonWeekGrid.Values.Where(v => v > 1).ToList();
    int extraTasks = conflicts.Sum(v => v - 1);
    int affectedCells = conflicts.Count;

    Console.WriteLine($"--- {label} ANALYSIS ---");
    Console.WriteLine($"[OVERALL] Fitness Score: {totalFitness:F4}");
    Console.WriteLine($"[DETAILS] Cnf: {conflict:F2} | Mov: {movement:F2} | Foc: {focus:F2} | Con: {continuity:F2} | Dur: {duration:F2}");
    
    if (extraTasks > 0)
    {
        Console.WriteLine($"[CONFLICT INFO] Extra Tasks: {extraTasks} | Affected Grids: {affectedCells} | Overtime: {extraTasks * 40}h");
    }
    else
    {
        Console.WriteLine("[CONFLICT INFO] No Conflicts! 👍");
    }

    if (showLine) Console.WriteLine(new string('-', 60));
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
