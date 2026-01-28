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
        var header = lines[0].Split(',');
        
        int personHeader = Array.IndexOf(header, "Person");
        int projectHeader = Array.IndexOf(header, "Project");

        foreach (var line in lines.Skip(1))
        {
            var cells = line.Split(',');

            string personName = cells[personHeader].Trim();
            string projectName = cells[personHeader].Trim();

            bool assigned = cells.Skip(2).Any(c => c.Trim().Equals("X", StringComparison.OrdinalIgnoreCase));
            if (!assigned) continue;

            if (!peopleByName.ContainsKey(personName))
                peopleByName[personName] = new Person(personName);

            if (!projectsByName.ContainsKey(projectName))
                projectsByName[projectName] = new Project(projectName);

            var person = peopleByName[personName];
            var project = projectsByName[projectName];

            person.projects.Add(project);
            project.people.Add(person);
        }

        return (peopleByName.Values.ToList(), projectsByName.Values.ToList());
    }
}
