using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using static Person;
using static Project;

/* 
This is a class dedicated to loading/parsing the raw data (csv).

It requires a file path in order to start reading the data.
The way this loader parses the data into objects is tailored according to the 
test data produced, so any new data needs to match the existing formatting.

The data is then parsed into lists of Project and People objects, which are then returned.
*/
public class Loader
{
    /// <summary>
    /// In order to run this method, a file path containing valid data is required.
    /// 
    /// After parsing csv data into a People and Projects object list, this method returns those lists.
    /// </summary>
    public (List<Person> people, List<Project> projects) LoadData(string filePath)
    {
        // reads all lines in the raw data (csv) and stores it as a string array of lines
        var lines = File.ReadAllLines(filePath);

        // initiate the people / projects dictionaries for name lookup (only used for reference)
        var peopleByName = new Dictionary<string, Person>();
        var projectsByName = new Dictionary<string, Project>();
        // splits the first line (the headers) and stores each header in the string array
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

            // stores the role of that person in the current row
            string RoleName = cells[roleHeader].Trim();

            // get the start and end data but searching cells for the specific placeholders
            var startDate = Array.IndexOf(cells, "s");
            var endDate = Array.IndexOf(cells, "e");

            // search dictionary for person (using name as key)
            if (!peopleByName.ContainsKey(personName))
                // if new person create new person object with role
                peopleByName[personName] = new Person(personName, RoleName);

            // search dictionary for project (using name as key)
            if (!projectsByName.ContainsKey(projectName))
                // if new project, create new project object with specified start/end dates
                projectsByName[projectName] = new Project(projectName, startDate - 2, endDate - 2);
            
            
            var person = peopleByName[personName];
            var project = projectsByName[projectName];

            Dictionary<int, int> weekWorkingHours = new Dictionary<int, int>();
            for (int i = startDate + 1; i < endDate; i++)
            {
                if (int.TryParse(cells[i], out int hours))
                {
                    weekWorkingHours.Add(i - 2, Convert.ToInt32(cells[i]));
                }
            }

            person.projects.Add(project, weekWorkingHours);
            project.people.Add(person);
            project.originalPeopleIds.Add(person.id);
            project.updateCapacity();
            project.OriginalDurationSpan = project.durationProjectFinder();            
        }

        return (peopleByName.Values.ToList(), projectsByName.Values.ToList());
    }
}
