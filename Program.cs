using static Project;
using static Person;
using static Loader;
using System;
using System.Collections.Generic;
using Google.OrTools.LinearSolver;
public class Program
{
    List<Project> projects = new List<Project>();
    List<Person> people = new List<Person>();

    public Program()
    {
        loadData();
    }

    public void loadData()
    {
        Loader load = new Loader();
        (this.people, this.projects) = load.LoadData("data/schedule_target75_small.csv");
        Console.WriteLine("Loaded.");
        testPrint();
    }

    public void testPrint()
    {
        foreach (var p in people)
        {
           Console.Write(p.name);
        }
        foreach (var p in projects)
        {
            Console.Write(p.name);
        }
    }
    static void Main(string[] args)
    {
       new Program();
    }
}