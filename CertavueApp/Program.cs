using static Project;
using static Person;
using static Loader;
using System;
using System.Collections.Generic;
public class Program
{
    List<Project> projects = new List<Project>();
    List<Person> people = new List<Person>();

    public Program()
    {
        loadData();
        testPrint();
    }

    public void loadData()
    {
        Loader load = new Loader();
<<<<<<< HEAD
        (this.people, this.projects) = load.LoadData("Data/schedule_target75_small.csv"); 
=======
        (this.people, this.projects) = load.LoadData("Data\\schedule_target75_small.csv");
>>>>>>> GreedyAlg
        Console.WriteLine("Loaded.");
    }


    public void testPrint()
    {
        Console.WriteLine("People:");
        foreach (var p in people)
        {
            Console.WriteLine("- " + p.name);
        }
        Console.WriteLine("Projects:");
        foreach (var p in projects)
        {
            Console.WriteLine("- " + p.name);
        }
        Console.WriteLine("Count of projects: " + projects.Count);
    }
    static void Main(string[] args)
    {
        new Program();
    }
}
