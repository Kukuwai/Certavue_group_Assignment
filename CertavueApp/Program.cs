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
        // loading data in
        dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule_target75_paired_extreme.csv");
        var originalState = loadData(dataPath);

        // export original data to html output
        Output output = new Output();
        output.ExportToHtml(dataPath, originalState, "Original");
        printStats("Original Data");

        // greedy algorithm starts, inluding export of output to html
        var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
        output.ExportToHtml(dataPath, scheduleAfterGreedy, "after_greedy");

        // moveByConflict method (manual optimisation)
        var scheduleAfterConflict = new MoveByConflict().start(scheduleAfterGreedy, projects);
        output.ExportToHtml(dataPath, scheduleAfterConflict, "after_conflict");

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
