using System;
using System.Collections.Generic;
using System.Linq;

public class RoleOptimizer
{
    // --- 这里是你之前补上的 Optimize 方法，我已经帮你修好了类型 ---
    public OptimizationResult Optimize(ScheduleState initialState, int maxPasses = 100)
    {
        var currentState = initialState; 
        var handler = new ScheduleHandler(currentState);
        double currentFitness = handler.CalculateFitnessScore(currentState);
        bool improvementFound = true;
        int passes = 0;

        int maxWeek = currentState.PersonWeekGrid.Keys.Any() ? currentState.PersonWeekGrid.Keys.Max(k => k.Week) : 0;

        while (improvementFound && passes < maxPasses)
        {
            improvementFound = false;
            passes++;

            for (int week = 0; week <= maxWeek; week++)
            {
                var conflicts = BuildConflictTasks(currentState, week);
                
                foreach (var task in conflicts)
                {
                    var candidates = GetReplacementCandidates(currentState, task);

                    foreach (var candidate in candidates)
                    {
                        Person originalPerson = task.OverloadedPerson;
                        
                        // TryMove 内部会修改 state
                        bool moveSuccess = TryMove(currentState, task, candidate);

                        if (moveSuccess)
                        {
                            double newFitness = handler.CalculateFitnessScore(currentState);

                            if (newFitness > currentFitness)
                            {
                                currentFitness = newFitness;
                                improvementFound = true;
                                break; 
                            }
                            else
                            {
                                // 回滚
                                int rawWeek = task.Week - currentState.GetShift(task.Project);
                                GreedyAlg.MoveWeekToReplacement(currentState, task.Project, candidate, originalPerson, rawWeek);
                            }
                        }
                    }
                }
            }
        }

        return new OptimizationResult
        {
            Improved = passes > 1,
            FinalFitness = currentFitness,
            BestState = currentState,
            CombinationsChecked = passes
        };
    }

    private List<ConflictTask> BuildConflictTasks(ScheduleState state, int week)
    {
        // 找出这一周工作项目超过 1 个的人
        List<int> overloadedIds = state.PersonWeekGrid
            .Where(kv => kv.Key.Week == week && kv.Value > 1)
            .Select(kv => kv.Key.PersonId)
            .Distinct()
            .ToList();

        var tasks = new List<ConflictTask>();
        foreach (int personId in overloadedIds)
        {
            Person overloaded = state.People.FirstOrDefault(p => p.id == personId);
            if (overloaded == null) continue;

            foreach (Project project in state.Projects)
            {
                int rawWeek = week - state.GetShift(project);
                
                // --- 修复点：out List<int> ---
                if (overloaded.projects.TryGetValue(project, out List<int> weeks) && weeks.Contains(rawWeek))
                {
                    tasks.Add(new ConflictTask
                    {
                        Week = week,
                        OverloadedPerson = overloaded,
                        Project = project
                    });
                }
            }
        }
        return tasks;
    }

    private List<Person> GetReplacementCandidates(ScheduleState state, ConflictTask task)
    {
        var candidates = new List<Person>();
        foreach (Person person in state.People)
        {
            if (person.id == task.OverloadedPerson.id) continue;

            if (!string.Equals(person.role, task.OverloadedPerson.role, StringComparison.OrdinalIgnoreCase)) continue;

            // --- 修复点：IsPersonFree 只传 3 个参数 ---
            if (!GreedyAlg.IsPersonFree(state, person, task.Week))
            {
                continue;
            }
            candidates.Add(person);
        }
        return candidates;
    }

    private bool TryMove(ScheduleState state, ConflictTask task, Person replacement)
    {
        int rawWeek = task.Week - state.GetShift(task.Project);

        // --- 修复点：out List<int> ---
        if (!task.OverloadedPerson.projects.TryGetValue(task.Project, out List<int> weeks))
        {
            return false;
        }
        if (!weeks.Contains(rawWeek))
        {
            return false;
        }

        // --- 修复点：IsPersonFree 只传 3 个参数 ---
        if (!GreedyAlg.IsPersonFree(state, replacement, task.Week))
        {
            return false;
        }

        GreedyAlg.MoveWeekToReplacement(state, task.Project, task.OverloadedPerson, replacement, rawWeek);

        // --- 修复点：out List<int> ---
        return replacement.projects.TryGetValue(task.Project, out List<int> replacementWeeks) && replacementWeeks.Contains(rawWeek);
    }
    
    private class ConflictTask
    {
        public int Week { get; set; }
        public Person OverloadedPerson { get; set; }
        public Project Project { get; set; }
    }

    public class OptimizationResult
    {
        public bool Improved { get; set; }
        public double FinalFitness { get; set; }
        public int CombinationsChecked { get; set; }
        public ScheduleState BestState { get; set; } // 确保定义了 BestState
    }

    // --- 修复点：Snapshot 内部结构改为 List ---
    private class AssignmentSnapshot
    {
        public Dictionary<Person, Dictionary<Project, List<int>>> PersonAssignments { get; set; }
        public Dictionary<Project, HashSet<Person>> ProjectPeople { get; set; }
    }
}