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
        // var beforeGreedy = new ScheduleState(people, projects);
        // GreedyChecker("Before Greedy", beforeGreedy);
        var greedy = new GreedyAlg();
        var state = greedy.StartGreedy(people, projects);
        // var afterGreedy = new ScheduleState(people, projects);
        //  GreedyChecker("After Greedy", afterGreedy);
    }

    public void loadData()
    {
        Loader load = new Loader();
        (this.people, this.projects) = load.LoadData("Data/schedule_target75_large.csv");
        Console.WriteLine("Loaded.");
    }

    //checking in GreedyAlg now
    // private void GreedyChecker(string label, ScheduleState state)
    // {
    //     int totalAssignments = state.PersonWeekGrid.Values.Sum();
    //     int nonConflictAssignments = state.PersonWeekGrid
    //         .Where(kv => kv.Value == 1)
    //         .Sum(kv => kv.Value);
    //     int doubleBooked = state.PersonWeekGrid.Count(kv => kv.Value >= 2);

    //     double pctNotDoubleBooked = totalAssignments == 0
    //         ? 100
    //         : (double)nonConflictAssignments / totalAssignments * 100;

    //     Console.WriteLine(label + " Total assignments=" + totalAssignments
    //         + " Double-booked weeks=" + doubleBooked
    //         + " % not double-booked=" + pctNotDoubleBooked.ToString("0.##"));
    // }

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
