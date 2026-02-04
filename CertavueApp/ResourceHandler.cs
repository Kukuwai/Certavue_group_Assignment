using System;
using System.Collections.Generic;
using System.Linq;

public class ScheduleHandler
{
    private readonly ScheduleState _state;
    private readonly AvailabilityFinder _finder;

    public ScheduleHandler(ScheduleState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        // 自动关联 AvailabilityFinder，方便算法查询谁有空
        _finder = new AvailabilityFinder(_state);
    }

    // --- First stage : get newest state ---

    public ScheduleState GetCurrentState() => _state;
    public AvailabilityFinder GetFinder() => _finder;

    // --- 第二阶段：运行中核心逻辑（算法最常调用） ---

    /// 获取某个项目的所有合法平移选项（由 State 的业务规则决定）
    public List<int> GetValidOptions(Project p)
    {
        return _state.GetValidShifts(p);
    }

    //----hold each person's gap period
    public Dictionary<string, string> GetGapsPerPerson()
   {
    var report = new Dictionary<string, string>();
    foreach (var p in _state.People)
    {
        // 1. 调用你现有的方法获取空闲周数列表
        List<int> freeWeeks = GetAvailableWeeksForPerson(p.name);
        
        // 2. 将列表转化成易读的格式，比如 "1, 2, 3, 10, 11"
        report[p.name] = string.Join(", ", freeWeeks);
    }
    return report;
    }

    public ShiftScore EvaluateMove(Project p, int candidateShift)
    {
        int currentShift = _state.GetShift(p);
        
        // 如果候选位移就是当前位移，直接返回 0 变化的 Score
        if (candidateShift == currentShift)
        {
            return new ShiftScore { DeltaDoubleBooked = 0, OverlapAfter = 0, ShiftDistance = 0 };
        }

        // 1. 获取当前占用格子的快照和候选格子的快照
        var currentCells = _state.GetGrid(p, currentShift);
        var candidateCells = _state.GetGrid(p, candidateShift);

        // 2. 找到所有受影响的格子（两者的并集）
        var touchedKeys = currentCells.Union(candidateCells).Distinct();

        int delta = 0;
        int overlapAfter = 0;

        // 3. 增量计算冲突变化
        foreach (var key in touchedKeys)
        {
            _state.PersonWeekGrid.TryGetValue(key, out int currentTotalCount);

            // 计算该格子在“移除当前项目”后的基数
            bool isOccupiedInCurrent = currentCells.Any(c => c.Equals(key));
            int baseCountWithoutProject = isOccupiedInCurrent ? currentTotalCount - 1 : currentTotalCount;

            // 计算该格子在“加入候选项目”后的新总数
            bool isOccupiedInCandidate = candidateCells.Any(c => c.Equals(key));
            int newCountWithCandidate = isOccupiedInCandidate ? baseCountWithoutProject + 1 : baseCountWithoutProject;

            // 判断冲突变化 (>= 2 为双重占用)
            if (currentTotalCount >= 2) delta -= 1; // 原本有冲突，计数减 1
            if (newCountWithCandidate >= 2)
            {
                delta += 1; // 移动后有冲突，计数加 1
                overlapAfter++; // 记录移动后该项目参与的冲突格子数
            }
        }

        return new ShiftScore
        {
            DeltaDoubleBooked = delta,
            OverlapAfter = overlapAfter,
            ShiftDistance = Math.Abs(candidateShift - currentShift)
        };
    }


    public void ExecuteMove(Project p, int newShift)
    {
        _state.ApplyShift(p, newShift);
    }

    // --- 第三阶段：总结与输出（为 LLM 和汇报准备） ---

    /// <summary>
    /// 生成详细的统计摘要。算法每一轮（Pass）结束或最终结束时调用。
    /// </summary>
    public string GenerateSummary()
    {
        // 统计全图
        int totalSlots = _state.PersonWeekGrid.Values.Sum();
        int conflictCells = _state.PersonWeekGrid.Values.Count(v => v >= 2);
        int cleanSlots = _state.PersonWeekGrid.Values.Count(v => v == 1);
        
        double successPct = totalSlots == 0 ? 100 : (double)cleanSlots / _state.PersonWeekGrid.Count * 100;

        // 统计负载最高的人（Top 3）
        var heavyLifters = _state.People
            .Select(p => new { p.name, Overload = _finder.CountOverloadedWeeks(p.name) })
            .Where(x => x.Overload > 0)
            .OrderByDescending(x => x.Overload)
            .Take(3)
            .Select(x => $"{x.name}({x.Overload} wks)");

        string report = $"[Summary] Conflicts: {conflictCells}, Success Rate: {successPct:0.##}%. ";
        if (heavyLifters.Any())
            report += "Critical Overload: " + string.Join(", ", heavyLifters);
        else
            report += "All staff within capacity.";

        return report;
    }

    public ScheduleState Finalize()
    {
        // 可以在这里执行最终的合法性检查
        return _state;
    }
}

// 辅助评分结构
public class ShiftScore
{
    public int DeltaDoubleBooked { get; set; } // 冲突的变化量，负数代表冲突减少（好）
    public int OverlapAfter { get; set; }     // 移动后该项目自身还剩多少冲突
    public int ShiftDistance { get; set; }     // 移动的距离（通常希望越小越好，保持紧凑）
}


