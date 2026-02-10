using System.Text;
using System.IO;

public class Output
{
    public Output()
    {
        
    }


    public void ExportToHtml(string originalFileName, ScheduleState state, string newFileName)
    {
        // Save alongside input data instead of bin output
        var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "Output"));
        Directory.CreateDirectory(dataDirectory);

        try
        {
            // generate the HTML filename based on the input CSV filename
            string exportFileName = originalFileName.Replace(".csv", "_Heatmap.html");
            exportFileName = exportFileName.Replace("_Heatmap.html", "_" + newFileName + "_Heatmap.html");
            var sb = new StringBuilder();

            //  CSS outlook 
            sb.Append("<html><head><meta charset='UTF-8'><style>");
            sb.Append("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; padding: 20px; }");
            sb.Append("h2 { color: #2c3e50; text-align: center; }");
            sb.Append("table { border-collapse: collapse; background: white; box-shadow: 0 4px 15px rgba(0,0,0,0.1); width: 100%; margin-top: 20px; }");
            sb.Append("th { background-color: #2c3e50; color: white; padding: 12px 8px; position: sticky; top: 0; z-index: 10; font-size: 12px; }");
            sb.Append("td { border: 1px solid #eee; padding: 8px; text-align: center; font-size: 11px; }");
            sb.Append(".name-col { text-align: left; font-weight: bold; background: #ebf0f1; position: sticky; left: 0; z-index: 5; min-width: 120px; }");
            sb.Append(".proj-col { text-align: left; background: #fff; position: sticky; left: 120px; z-index: 4; border-right: 2px solid #ddd; min-width: 150px; }");
            sb.Append(".role-col { text-align: left; background: #fff; position: sticky; left: 270px; z-index: 3; border-right: 2px solid #ddd; min-width: 140px; }");
            sb.Append(".work { background-color: #3498db; color: white; font-weight: bold; }");
            sb.Append("tr:hover { background-color: #f1f1f1; }");
            sb.Append("</style></head><body>");

            sb.Append("<h2>Project Scheduling.</h2>");
            sb.Append("<table><tr><th class='name-col'>Person</th><th class='proj-col'>Project</th><th class='role-col'>Role</th>");
            for (int w = 1; w <= 52; w++) sb.Append($"<th>W{w}</th>");
            sb.Append("</tr>");

            // 2. align on Alg logic to sort
            var allAssignments = new List<AssignmentRow>();

            foreach (var person in state.People)
            {
                foreach (var proj in state.Projects)
                {
                
                    var grid = state.GetGrid(proj, state.GetShift(proj));
                    
                    // Filter out the weeks assigned to the current person
                    var personWeeks = grid.Where(k => k.PersonId == person.id).Select(k => k.Week).ToList();

                    if (personWeeks.Any())
                    {
                        allAssignments.Add(new AssignmentRow
                        {
                            PersonName = person.name,
                            ProjectName = proj.name,
                            PersonRole = person.role,
                            StartWeek = personWeeks.Min(),
                            Duration = state.GetDuration(proj), 
                            PeopleCount = proj.people.Count,   
                            ActiveWeeks = personWeeks.ToHashSet()
                        });
                    }
                }
            }

        
            // Sort by duration descending; if durations are equal, sort by headcount descending
            var sortedAssignments = allAssignments
                .OrderByDescending(a => a.Duration)   // align on Alg method：OrderByDescending(p => state.GetDuration(p))
                .ThenByDescending(a => a.PeopleCount) // same with Alg method：ThenByDescending(p => p.people.Count)
                .ThenBy(a => a.StartWeek)            
                .ThenBy(a => a.PersonName)            
                .ToList();

            // 4. create html table
            foreach (var row in sortedAssignments)
            {
                sb.Append("<tr>");
                sb.Append($"<td class='name-col'>{row.PersonName}</td>");
                sb.Append($"<td class='proj-col'>{row.ProjectName}</td>");
                sb.Append($"<td class='role-col'>{row.PersonRole}</td>");

                for (int w = 1; w <= 52; w++)
                {
                    bool hasWork = row.ActiveWeeks.Contains(w);
                    string cellClass = hasWork ? "class='work'" : "";
                    string cellValue = hasWork ? "40" : ""; 
                    sb.Append($"<td {cellClass}>{cellValue}</td>");
                }
                sb.Append("</tr>");
            }

            sb.Append("</table></body></html>");

            // 5. write to file
            string exportPath = Path.Combine(dataDirectory, Path.GetFileName(exportFileName));
            File.WriteAllText(exportPath, sb.ToString());
            Console.WriteLine($"\n[Success] The sorted heatmap has been saved: {exportPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Export failed: " + ex.Message);
        }
    }

    public void ExportToHtml(string originalFileName, ScheduleState state)
    {
        var dataDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data"));
        Directory.CreateDirectory(dataDirectory);

        try
        {
            // generate the HTML filename based on the input CSV filename
            string exportFileName = originalFileName.Replace(".csv", "_Heatmap.html");
            var sb = new StringBuilder();

            //  CSS outlook 
            sb.Append("<html><head><meta charset='UTF-8'><style>");
            sb.Append("body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f7f6; padding: 20px; }");
            sb.Append("h2 { color: #2c3e50; text-align: center; }");
            sb.Append("table { border-collapse: collapse; background: white; box-shadow: 0 4px 15px rgba(0,0,0,0.1); width: 100%; margin-top: 20px; }");
            sb.Append("th { background-color: #2c3e50; color: white; padding: 12px 8px; position: sticky; top: 0; z-index: 10; font-size: 12px; }");
            sb.Append("td { border: 1px solid #eee; padding: 8px; text-align: center; font-size: 11px; }");
            sb.Append(".name-col { text-align: left; font-weight: bold; background: #ebf0f1; position: sticky; left: 0; z-index: 5; min-width: 120px; }");
            sb.Append(".proj-col { text-align: left; background: #fff; position: sticky; left: 120px; z-index: 4; border-right: 2px solid #ddd; min-width: 150px; }");
            sb.Append(".work { background-color: #3498db; color: white; font-weight: bold; }");
            sb.Append("tr:hover { background-color: #f1f1f1; }");
            sb.Append("</style></head><body>");

            sb.Append("<h2>Project Scheduling.</h2>");
            sb.Append("<table><tr><th class='name-col'>Person</th><th class='proj-col'>Project</th>");
            for (int w = 1; w <= 52; w++) sb.Append($"<th>W{w}</th>");
            sb.Append("</tr>");

            // 2. align on Alg logic to sort
            var allAssignments = new List<AssignmentRow>();

            foreach (var person in state.People)
            {
                foreach (var proj in state.Projects)
                {
                
                    var grid = state.GetGrid(proj, state.GetShift(proj));
                    
                    // Filter out the weeks assigned to the current person
                    var personWeeks = grid.Where(k => k.PersonId == person.id).Select(k => k.Week).ToList();

                    if (personWeeks.Any())
                    {
                        allAssignments.Add(new AssignmentRow
                        {
                            PersonName = person.name,
                            ProjectName = proj.name,
                            StartWeek = personWeeks.Min(),
                            Duration = state.GetDuration(proj), 
                            PeopleCount = proj.people.Count,   
                            ActiveWeeks = personWeeks.ToHashSet()
                        });
                    }
                }
            }

        
            // Sort by duration descending; if durations are equal, sort by headcount descending
            var sortedAssignments = allAssignments
                .OrderByDescending(a => a.Duration)   // align on Alg method：OrderByDescending(p => state.GetDuration(p))
                .ThenByDescending(a => a.PeopleCount) // same with Alg method：ThenByDescending(p => p.people.Count)
                .ThenBy(a => a.StartWeek)            
                .ThenBy(a => a.PersonName)            
                .ToList();

            // 4. create html table
            foreach (var row in sortedAssignments)
            {
                sb.Append("<tr>");
                sb.Append($"<td class='name-col'>{row.PersonName}</td>");
                sb.Append($"<td class='proj-col'>{row.ProjectName}</td>");

                for (int w = 1; w <= 52; w++)
                {
                    bool hasWork = row.ActiveWeeks.Contains(w);
                    string cellClass = hasWork ? "class='work'" : "";
                    string cellValue = hasWork ? "40" : ""; 
                    sb.Append($"<td {cellClass}>{cellValue}</td>");
                }
                sb.Append("</tr>");
            }

            sb.Append("</table></body></html>");

            // 5. write to file
            string exportPath = Path.Combine(dataDirectory, Path.GetFileName(exportFileName));
            File.WriteAllText(exportPath, sb.ToString());
            //Console.WriteLine($"\n[Success] The sorted heatmap has been saved: {exportPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine("Export failed: " + ex.Message);
        }
    }


}
