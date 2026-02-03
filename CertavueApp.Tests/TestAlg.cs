using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;

public class TestAlg 
{
    [Fact]
    public void TestGreedyWithCsvData() 
    {
        // --- 这一步不能少：准备数据 ---
        var loader = new Loader();
        string testFilePath = "Data/SmallTestSet.csv"; 
        var (people, projects) = loader.LoadData(testFilePath);

        // --- 核心修复：定义 state 和 greedy 变量 ---
        var state = new ScheduleState(people, projects); // 定义 state
        var greedy = new GreedyAlg();                    // 定义 greedy

        // 记录开始前的数据 (现在 state 存在了)
        int startConflicts = state.PersonWeekGrid.Values.Count(v => v >= 2);

        // 运行算法
        greedy.BuildGreedySchedule(state);

        // 计算结果数据
        int finalConflicts = state.PersonWeekGrid.Values.Count(v => v >= 2);
        int totalSlots = state.PersonWeekGrid.Values.Count();
        int cleanSlots = state.PersonWeekGrid.Values.Count(v => v == 1);
        double successRate = totalSlots == 0 ? 100 : (double)cleanSlots / totalSlots * 100;

        // 输出报告
        Console.WriteLine("\n" + new string('=', 30));
        Console.WriteLine($"【Alg test report】");
        Console.WriteLine($"1. Initial Conflict Weeks: {startConflicts}");
        Console.WriteLine($"2. Remaining Conflicts After Optimization: {finalConflicts}");
        Console.WriteLine($"3. Final Success Rate: {successRate:0.##}%");
        Console.WriteLine(new string('=', 30) + "\n");
    }
}