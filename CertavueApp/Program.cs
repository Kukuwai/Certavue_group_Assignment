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
        output.ExportToHtml(dataPath, originalState, "Original");

        var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
        output.ExportToHtml(dataPath, scheduleAfterGreedy, "after_greedy");
        testAlgo(scheduleAfterGreedy, "After Greedy");


    }

    public ScheduleState loadData(string path)
    {
        Loader load = new Loader();
        (var people, var projects) = load.LoadData(path);
        var state = new ScheduleState(people, projects);
        this.people = people;
        this.projects = projects;
        Console.WriteLine("Loaded.\n");
        return state;
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
    public string PersonRole { get; internal set; }
}
