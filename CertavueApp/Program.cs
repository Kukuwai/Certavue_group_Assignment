﻿using static Project;
using static Person;
using static Loader;
using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.IO.Pipes;
public class Program
{
    List<Project> projects = new List<Project>();
    List<Person> people = new List<Person>();

    public Program()
    {
        loadData();
        //testPrint();
        var beforeGreedy = new ScheduleState(people, projects);
        GreedyChecker("Before Greedy", beforeGreedy);
        new GreedyAlg().StartGreedy(people, projects);
        var afterGreedy = new ScheduleState(people, projects);
        GreedyChecker("After Greedy", afterGreedy);
    }

    public void loadData()
    {
        Loader load = new Loader();
        (this.people, this.projects) = load.LoadData("Data/schedule_target75_large.csv");
        Console.WriteLine("Loaded.");
    }


    private void GreedyChecker(string label, ScheduleState state)
    {
        int doubleBooked = state.PersonWeekGrid.Count(kv => kv.Value > 1);
        int total = state.PersonWeekGrid.Count;
        double pctClear = total == 0 ? 100 : (double)(total - doubleBooked) / total * 100;
        Console.WriteLine(label + " Double-booked= " + doubleBooked + " % not double-booked= " + pctClear);
    }
    public void testPrint()
    {
        // Console.WriteLine("People:");
        // foreach (var p in people)
        // {
        //     Console.WriteLine("- " + p.name);
        //     foreach (KeyValuePair<Project, List<int>> kvp in p.projects)
        //     {
        //         List<int> values = kvp.Value;
        //         foreach (var v in values)
        //         {
        //             Console.WriteLine("Key = {0}, Value = {1}", kvp.Key.name, v);
        //         }
        //     }

        // }
        // Console.WriteLine("Count of projects: " + projects.Count);
    }
    static void Main(string[] args)
    {
        new Program();
    }
}