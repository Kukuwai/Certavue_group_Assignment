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

            // greedy algorithm starts, inluding export of output to html
            Console.WriteLine($"Greeding Running File - {System.IO.Path.GetFileName(file)}\n");
            var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
            output.ExportToHtml(file, scheduleAfterGreedy, "after_greedy");


            var roleOpt = new RoleOptimizer();
            var roleResult = roleOpt.Optimize(scheduleAfterGreedy, maxPasses: 999999999);
            Program.LatestState = roleResult.BestState;// * add newest state
            output.ExportToHtml(file, scheduleAfterGreedy, "After Role Checks");
            printStats("Role optimiser Data", roleResult.BestState, file, true);

            projects[0].printPeopleOnProject();
            Console.WriteLine("-------");
            people[0].printProjectsForPerson();

            finalState = roleResult.BestState;
            
        }
        ProcessNewProjectInsertion(finalState);
    }

    private void ProcessNewProjectInsertion(ScheduleState currentState){
     if (currentState == null)
    {
        Console.WriteLine("[Error] 没有找到可用的全局优化状态，无法插入新项目。");
        return;
    }

    // 2. 确定存放“待添加项目”的路径
    var newProjectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AddNewProject"));
    Console.WriteLine($"\n[ACTION] 正在搜索新项目文件: {newProjectDir}");

    if (!Directory.Exists(newProjectDir))
    {
        Console.WriteLine("[Error] 找不到 AddNewProject 文件夹。");
        return;
    }

    // 3. 初始化处理器（它会基于当前的 currentState 进行打分）
    ScheduleHandler handler = new ScheduleHandler(currentState);
    string[] newFiles = Directory.GetFiles(newProjectDir, "*.csv");

    foreach (var file in newFiles)
    {
        Console.WriteLine($"[File] 正在处理: {Path.GetFileName(file)}");
        
        List<Project> newProjects = LoadNewProjectsOnly(file);

        foreach (var project in newProjects)
        {
            double scoreDelta = handler.EvaluateNewProjectInsertion(project);

            if (scoreDelta >= 0)
            {
                Console.WriteLine($"   ✅ [Success] 项目 '{project.name}' 已插入。分数提升/变化: {scoreDelta:F4}");
            }
            else
            {
                Console.WriteLine($"   ⚠️ [Warning] 项目 '{project.name}' 插入后分数下降 ({scoreDelta:F4})，请检查资源冲突。");
            }
        }
    }

    printStats("FINAL SCHEDULE (After New Project Insertion)", currentState, "Global_Result", true);
    }

    public List<Project> LoadNewProjectsOnly(string path)
    {
        Loader load = new Loader();
        // 只取返回元组的第二个值（Projects）
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
