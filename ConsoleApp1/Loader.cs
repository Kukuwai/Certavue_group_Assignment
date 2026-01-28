using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class Loader
{
    public (List<Person> people, List<Project> projects) LoadData(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        foreach (var l in lines)
        {
            Console.Write(l);
        }
        var peopleByName = new Dictionary<string, Person>();
        var projectsByName = new Dictionary<string, Project>();

        return (peopleByName.Values.ToList(), projectsByName.Values.ToList());


    }
}
