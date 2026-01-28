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
        (this.people, this.projects) = load.LoadData("/Users/millar/Certavue/ConsoleApp1/data/resource_allocation.csv");
        Console.WriteLine("Loaded.");
    }

    public void testPrint()
    {
        foreach (var p in people)
        {
           Console.Write(p.name);
        }
    }
    static void Main(string[] args)
    {
       new Program();
    }
}