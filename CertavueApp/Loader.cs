using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using static Person;
using static Project;

public class Loader
{
    public (List<Person> people, List<Project> projects) LoadData(string filePath)
    {
        var lines = File.ReadAllLines(filePath);

        // initiate the people / projects dictionaries
        var peopleByName = new Dictionary<string, Person>();
        var projectsByName = new Dictionary<string, Project>();
        var header = lines[0].Split(',');
        // searches the index for the headers i.e people name and project name (this should always be 0 and 1)
        int personHeader = Array.IndexOf(header, "Person");
        int projectHeader = Array.IndexOf(header, "Project");
        int roleHeader = Array.IndexOf(header, "Role");

        // skips the headers, so reads in the rows (our actual data i.e capacity)
        foreach (var line in lines.Skip(1))
        {
            // splits each line so that it represents the cells in excel
            var cells = line.Split(',');

            // stores the name of the person in the current row
            string personName = cells[personHeader].Trim();
            // stores the name of the project in the current row
            string projectName = cells[projectHeader].Trim();

            // stores the name of the project in the current row
            string RoleName = cells[roleHeader].Trim();

            var startDate = Array.IndexOf(cells, "s");
            var endDate = Array.IndexOf(cells, "e");

            if (!peopleByName.ContainsKey(personName))
                peopleByName[personName] = new Person(personName, 40, RoleName); // Menka added the capacity as default 40 hours per week.

            if (!projectsByName.ContainsKey(projectName))
                projectsByName[projectName] = new Project(projectName, startDate - 2, endDate - 2);

            var person = peopleByName[personName];
            var project = projectsByName[projectName];

            Dictionary<int, int> weekWorkingHours = new Dictionary<int, int>();

            for (int i = startDate + 1; i < endDate; i++)
            {
                if (int.TryParse(cells[i].Trim(), out int hours) && hours > 0)
                {
                    weekWorkingHours.Add(i - 2, hours);
                }
            }

            // Dictionary<int, int> weekWorkingHours = new Dictionary<int, int>();
            // for (int i = startDate + 1; i < endDate; i++)
            // {
            //     if (int.TryParse(cells[i], out int hours))
            //     {
            //         weekWorkingHours.Add(i - 2, Convert.ToInt32(cells[i]));
            //     }
            // }
            person.projects.Add(project, weekWorkingHours);
            project.people.Add(person);
            project.originalPeopleIds.Add(person.id);
            project.updateCapacity();

        }
        return (peopleByName.Values.ToList(), projectsByName.Values.ToList());
    }
}
