using Xunit;
using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Text;
using static Output;

public class TestOutput
{
[Fact]
public void TestAlgorithmToCsvAndHtml()
{
    // --- 1. 准备数据 ---
    string csvPath = GetTestCsvPath(); 
    var loader = new Loader();
    var (people, projects) = loader.LoadData(csvPath);
    var state = new ScheduleState(people, projects);

    // --- 2. 运行算法 ---
    new GreedyAlg().BuildGreedySchedule(state);

    // --- 3. 构造排序数据 (提取这部分逻辑) ---
    var allAssignments = new List<AssignmentRow>();
    foreach (var person in state.People)
    {
        foreach (var proj in state.Projects)
        {
            var grid = state.GetGrid(proj, state.GetShift(proj));
            var personWeeks = grid.Where(k => k.PersonId == person.id).Select(k => k.Week).ToList();
            if (personWeeks.Any())
            {
                allAssignments.Add(new AssignmentRow {
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

    // 执行排序
    var sorted = allAssignments
        .OrderByDescending(a => a.Duration)
        .ThenByDescending(a => a.PeopleCount)
        .ToList();

    // --- 4. 同时输出两种格式 ---
    Output output = new Output();
    output.ExportToHtml(csvPath, state); // 输出热力图

    // --- 5. 验证 ---
    string expectedCsvPath = csvPath.Replace(".csv", "_Sorted.csv");
    Assert.True(File.Exists(expectedCsvPath));
}

    // 辅助方法：定位源码 Data 文件夹
    private string GetTestCsvPath() 
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        // 退三级回到源码根目录
        string projectRootDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", ".."));
        // 确保这里文件名是 SmallTestSet.csv
        return Path.Combine(projectRootDir, "Data", "schedule_target75_medium_with_roles_40s.csv");
    }
}