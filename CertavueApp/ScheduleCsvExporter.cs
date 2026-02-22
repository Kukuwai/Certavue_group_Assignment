using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

public static class ScheduleCsvExporter
{

    public static void ExportStateToWeeklyTableCsv(ScheduleState state, string outputPath)
    {
        StringBuilder sb = new StringBuilder(); //will be the csv
        sb.Append("Person,Project,Role"); //header columns and weeks
        int headerWeek = 1;
        while (headerWeek <= 52)
        {
            sb.Append(",W");
            sb.Append(headerWeek);
            headerWeek = headerWeek + 1;
        }
        sb.AppendLine();

        int projectIndex = 0; //to count the project iteration
        while (projectIndex < state.Projects.Count)
        {
            Project project = state.Projects[projectIndex];
            int shift = state.GetShift(project); //shift value for where it actually is
            foreach (Person person in project.people)
            {
                Dictionary<int, int> rawWeekHours; //per week hours
                bool hasProject = person.projects.TryGetValue(project, out rawWeekHours); //Persons hours dict for current project
                if (hasProject) //only need their project
                {
                    int[] hoursByWeek = new int[53]; //calendar array not using 0
                    foreach (KeyValuePair<int, int> kv in rawWeekHours)
                    {
                        int shiftedWeek = kv.Key + shift; //Convert to calendar week
                        int hours = kv.Value; //Hours for that week
                        if (hours > 0) // Ignores zero or negative entries.
                        {
                            if (shiftedWeek < 1) shiftedWeek = 1; // Clamp early weeks into first export column.
                            if (shiftedWeek > 52) shiftedWeek = 52; // Clamp late weeks into last export column.
                            hoursByWeek[shiftedWeek] = hoursByWeek[shiftedWeek] + hours; // Accumulates hours in case multiple entries land on same shifted week.
                        }
                    }
                    bool hasAnyHours = false; //Will be a blank row or not
                    int checkWeek = 1;
                    while (checkWeek <= 52)
                    {
                        if (hoursByWeek[checkWeek] > 0) //At last one empty week
                        {
                            hasAnyHours = true;
                            break;
                        }
                        checkWeek = checkWeek + 1; //Moves to next weeks
                    }
                    if (hasAnyHours) //only when at least one week has hours
                    {
                        sb.Append(EscapeCsv(person.name));
                        sb.Append(","); //Seperator 
                        sb.Append(EscapeCsv(project.name));
                        sb.Append(","); //Seperate
                        sb.Append(EscapeCsv(person.role));

                        int writeWeek = 1; //Writes columns
                        while (writeWeek <= 52)
                        {
                            sb.Append(","); //Seperates each week
                            if (hoursByWeek[writeWeek] > 0) //Blank if no hours
                            {
                                sb.Append(hoursByWeek[writeWeek]);
                            }
                            writeWeek = writeWeek + 1; //Advanbce week
                        }

                        sb.AppendLine(); //Ends row
                    }
                }
            }
            projectIndex = projectIndex + 1; //Move to next project
        }
        File.WriteAllText(outputPath, sb.ToString()); //Send file to target path


    }
    private static string EscapeCsv(string value) //csv formatting
    {
        bool needsQuotes = false; //checks if double quotes are needed
        if (value.IndexOf(',') >= 0) needsQuotes = true; //changes commas
        if (value.IndexOf('"') >= 0) needsQuotes = true; //changes quotes
        if (value.IndexOf('\n') >= 0) needsQuotes = true; //changes new lines
        if (value.IndexOf('\r') >= 0) needsQuotes = true; //carriage requires 

        if (needsQuotes == false)
        {
            return value;
        }

        string escaped = value.Replace("\"", "\"\"");
        return "\"" + escaped + "\"";
    }
}
