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

        dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule_target75_paired_extreme.csv");
        var originalState = loadData(dataPath);
        Output output = new Output();
        output.ExportToHtml(dataPath, originalState);
        testPrint(originalState);
        var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
        testPrint(scheduleAfterGreedy);
        testAlgo(scheduleAfterGreedy, "After Greedy");

        testAlgo(scheduleAfterGreedy, "CP-SAT start");

        var cp = new cpsat();
        var cpResult = cp.OptimizeShifts(scheduleAfterGreedy, 3);

        Console.WriteLine($"CP-SAT status: {cpResult.Status}");

        if (cpResult.Status == Google.OrTools.Sat.CpSolverStatus.Optimal ||
            cpResult.Status == Google.OrTools.Sat.CpSolverStatus.Feasible)
        {
            cp.ApplySolution(scheduleAfterGreedy, cpResult);
            testAlgo(scheduleAfterGreedy, "After CP-SAT");
        }
        else
        {
            Console.WriteLine("No feasible CP-SAT solution; kept Greedy schedule.");
        }

        //var scheduleAfterConflict = new MoveByConflict().start(scheduleAfterGreedy, projects);
        //testAlgo(scheduleAfterConflict, "After MoveByConflict");

    }

    /*public void StartApp()
    {
        loadData(); // 加载大数据
        var stateAfter = new GreedyAlg().StartGreedy(people, projects);
        string filePath = "Data/schedule_target75_large.csv";
        ExportToHtml(filePath, stateAfter);
        //var m = new MoveByConflict(stateAfter);
    }*/

    public ScheduleState loadData(string path)
    {
        Loader load = new Loader();
        (var people, var projects) = load.LoadData(path);
        var state = new ScheduleState(people, projects);
        this.people = people;
        this.projects = projects;
        Console.WriteLine("Loaded.");
        return state;
    }



    public void testPrint(ScheduleState state)
    {
        var handler = new ScheduleHandler(state);
        var score = handler.CalculateFitnessScore(state);
        Console.WriteLine("Fitness score: " + score);
    }


    public void testAlgo(ScheduleState state, string label)
    {
        int total = state.PersonWeekGrid.Values.Sum(); //only occupied person/weeks
        int notDoubleBookedCells = state.PersonWeekGrid.Where(kv => kv.Value == 1).Sum(kv => kv.Value);
        double pct;

        if (total == 0)
        {
            pct = 100.0;
        }
        else
        {
            pct = (double)notDoubleBookedCells / total * 100.0;
        }

        Console.WriteLine(label + " total: " + total + ", double-booked=" + (total - notDoubleBookedCells) + ", % not double-booked=" + pct.ToString("0.##"));
    }


    static void Main(string[] args)
    {
        new Program();
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
}
