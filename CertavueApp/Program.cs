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
        
        dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "schedule_target75_medium_with_roles_40s.csv");
        var originalState = loadData(dataPath);
        Output output = new Output();
        output.ExportToHtml(dataPath, originalState);
        testPrint(originalState);
        var scheduleAfterGreedy = new GreedyAlg().StartGreedy(people, projects);
        testAlgo(scheduleAfterGreedy);

        var scheduleAfterConflict = new MoveByConflict().start(scheduleAfterGreedy, projects);
        testAlgo(scheduleAfterConflict);
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
        foreach (var p in state.People)
        {
            Console.WriteLine("Name: " + p.id + " | Role: " + p.role);
        }
    }
    public void testPrint(List<Person> people)
    {
        foreach (var p in people)
        {

            Console.WriteLine("Name: " + p.id + " | Role: " + p.role);
        }
    }


    public void testAlgo(ScheduleState state)
    {
        // double booking count
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
