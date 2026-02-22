using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.OrTools.PDLP;

public class ScheduleHandler
{
    private readonly ScheduleState _state;
    private readonly AvailabilityFinder _finder;

    public ScheduleHandler(ScheduleState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _finder = new AvailabilityFinder(_state);
    }

    // the class to evaulate shift
    // summary : The Fitness Score is our overall target—it tells us how good the schedule is. 
    //But it doesn't tell us why it's bad.By adding RoleGapReport, we give the system 'diagnostic' power.
    // When the score drops, the $RoleGapReport$ tells the user: 'It's because you need 1 more Developer.' Without this, the user just sees a low score and won't know how to fix it.
    public class ShiftPerformance
    {
        public int Shift { get; set; }
        public double Score { get; set; }
        public string PrimaryConflict { get; set; }
        public bool IsOptimal { get; set; }
    }

    // the class to pass the gap of resource after shifting or simulate shift
    public class RoleGapReport
    {
        public double Saturation { get; set; }
        public int MissingHours { get; set; }
        public double RecommendedStaff { get; set; }
        public string Status => Saturation > 1.0 ? "Critical" : "Healthy";
    }


    //---------add fitness socre logic
    public class FitnessScore
    {
        public double ConflictWeight = 0.40;      // conflicts sorce
        public double MovementWeight = 0.20;      // movement sorce： measure weather a project move frequently to different time axis in order to reduce conflicts
        public double FocusWeight = 0.20;         // measure weather a person be shifted frequently
        public double ContinuityWeight = 0.10;    // measure weather a project has most continuity 
        public double DurationWeight = 0.10;      // project duration
    }

    //-------method of calculate fitness score
    public double CalculateFitnessScore(ScheduleState state)
    {
        double conflictScore = GetConflictScore(state);
        double movementScore = GetMovementScore(state);
        double focusScore = GetFocusScore(state);
        double continuityScore = GetContinuityScore(state);
        double durationScore = GetDurationScore(state);

        // Final weighted sum
        return (conflictScore * 0.5) +
               (movementScore * 0.2) +
               (focusScore * 0.2) +
               (continuityScore * 0.05) +
               (durationScore * 0.05);
    }

    // This is a overload punisher
    public double GetConflictScore(ScheduleState state)
    {
        if (state.PersonWeekHours.Count == 0) return 1.0;

        double totalOverworkHours = 0;
        double totalAssignedHours = 0;

        // create new dictionary for people and that personid for easy reference in loop below
        var peopleById = new Dictionary<int, Person>();
        foreach (Person p in state.People)
        {
            peopleById.Add(p.id, p);
        }

        // changed to iterate person week totals not person-project weeks assigned
        foreach (var personWeek in state.PersonWeekHours)
        {
            // get person id
            var personId = personWeek.Key.PersonId;
            // get assigned hours for that week / per person
            var assignedHours = personWeek.Value;

            int capacity = 40;
            // get person by id using new dictionary created above and check if capacity above 0
            if (peopleById.TryGetValue(personId, out var person) && person.capacity > 0)
            {
                capacity = person.capacity;
            }

            totalAssignedHours += assignedHours;
            totalOverworkHours += Math.Max(0, assignedHours - capacity);

            /* caculate the overwork hours
            if (hoursInThisCell > CAPACITY_LIMIT)
            { // caculate the total overwork hours
                totalOverworkHours += (hoursInThisCell - CAPACITY_LIMIT);
            }*/
        }
        // check if person is even assigned hours on project
        if (totalAssignedHours <= 0)
        {
            // if no return default score
            return 1.0;
        }

        //caculate percentage of overwork
        double conflictRatio = totalOverworkHours / totalAssignedHours;
        //Normalization
        return Math.Max(0, 1.0 - conflictRatio);
    }





    // public double GetMovementScore(ScheduleState state)
    // {
    //     // sum shift
    //     double totalShift = state.Projects.Sum(p => Math.Abs(state.GetShift(p)));
    //     // normalization socre
    //     double avgShift = totalShift / state.Projects.Count;
    //     return Math.Max(0, 1.0 - (avgShift / 4.0)); // if it near with 4 it will be 0
    // }

    public double GetMovementScore(ScheduleState state)
    {
        if (state.Projects.Count == 0) return 1.0;

        double weightedPenaltySum = 0.0;
        double weightSum = 0.0;

        foreach (Project project in state.Projects)
        {
            List<int> validShifts = state.GetValidShifts(project);
            int maxAllowedShift = validShifts.Count == 0 ? 0 : validShifts.Max(s => Math.Abs(s));
            int currentShift = Math.Abs(state.GetShift(project));

            double normalizedShift = maxAllowedShift == 0
                ? 0.0
                : Math.Min(1.0, (double)currentShift / maxAllowedShift);

            double weight = GetProjectEffortHours(project);
            weightedPenaltySum += normalizedShift * weight;
            weightSum += weight;
        }

        if (weightSum <= 0) return 1.0;

        double averagePenalty = weightedPenaltySum / weightSum;
        return Math.Max(0.0, 1.0 - averagePenalty);
    }

    private static double GetProjectEffortHours(Project project)
    {
        double total = 0.0;

        foreach (Person person in project.people)
        {
            if (!person.projects.TryGetValue(project, out Dictionary<int, int> weekHours))
                continue;

            total += weekHours.Values.Where(h => h > 0).Sum();
        }

        return total > 0 ? total : 1.0;
    }

    public void DebugConflictDetails(ScheduleState state)
    {
        int totalConflictWeeks = 0;   // 有冲突的周数
        double totalOvertimeHours = 0;
        var conflictingPeople = new HashSet<int>();

        Console.WriteLine("\n========== [DEBUG Analyze (Based on Hours)] ==========");

        // 统一使用 PersonWeekHours，这才是你的算法真正优化的维度
        foreach (var entry in state.PersonWeekHours)
        {
            var personId = entry.Key.PersonId;
            var week = entry.Key.Week;
            var assignedHours = entry.Value;

            // 获取该人的实际容量
            int capacity = 40;
            var person = state.People.FirstOrDefault(p => p.id == personId);
            if (person != null && person.capacity > 0) capacity = person.capacity;

            if (assignedHours > capacity)
            {
                totalConflictWeeks++;
                double overtime = assignedHours - capacity;
                totalOvertimeHours += overtime;
                conflictingPeople.Add(personId);

                // 获取该周有多少个项目（可选，用于辅助诊断）
                state.PersonWeekGrid.TryGetValue(new ScheduleState.WeekKey(personId, 0, week), out int projCount);

                Console.WriteLine($"[CONFLICTS🚨] Person ID: {personId.ToString().PadRight(4)} | Week: {week.ToString().PadRight(2)} | Hours: {assignedHours}h (Cap: {capacity}h) | Overtime: {overtime}h");
            }
        }

        if (totalConflictWeeks == 0)
        {
            Console.WriteLine(">>> Success！👍 No hour-based conflicts.");
        }
        else
        {
            Console.WriteLine("------------------------------------------");
            Console.WriteLine($"Affected Person-Weeks: {totalConflictWeeks}");
            Console.WriteLine($"Related Persons: {conflictingPeople.Count}");
            Console.WriteLine($"Total Overload Time: {totalOvertimeHours} hours.");
        }
        Console.WriteLine("==========================================\n");
    }

    // public double GetFocusScore(ScheduleState state)
    // {
    //     // add empty state check to match other methods above??
    //     if (state.PersonWeekGrid.Count == 0) return 1.0;

    //     // make new dictionary to count how many projects each person has / per week
    //     var projectsPerPersonPerWeek = new Dictionary<ScheduleState.PersonWeekKey, int>();

    //     // iterate ovver person week grid to populate new dictionary for counting
    //     foreach (var weekKey in state.PersonWeekGrid)
    //     {
    //         // skip any people with 0 weeks
    //         if (weekKey.Value <= 0)
    //         {
    //             continue;
    //         }
    //         // think we have to make each weekKey into a personWeekKey
    //         var keyOfWeekKey = new ScheduleState.PersonWeekKey(weekKey.Key.PersonId, weekKey.Key.Week);
    //         // check if new, if new start count at 0
    //         if (!projectsPerPersonPerWeek.ContainsKey(keyOfWeekKey))
    //         {
    //             projectsPerPersonPerWeek[keyOfWeekKey] = 0;
    //         }
    //         // if not new add 1 to count
    //         projectsPerPersonPerWeek[keyOfWeekKey] += 1;
    //     }

    //     // check for invalide dictinary, if no weekeys saved
    //     if (projectsPerPersonPerWeek.Count == 0)
    //     {
    //         return 1.0;
    //     }
    //     // need to do a count to see if there are multiple tasks someone is working on / per week
    //     var multiTaskWeeks = projectsPerPersonPerWeek.Values.Count(projectCount => projectCount > 1);

    //     // return the calc for focus score, or 0 if negative.
    //     return Math.Max(0, 1.0 - ((double)multiTaskWeeks / projectsPerPersonPerWeek.Count));


    //     /*
    //     // sum on average, how much projects each person takes on every week
    //     var multiTaskWeeks = state.PersonWeekGrid.Values.Count(v => v > 1);
    //     // normalization
    //     return Math.Max(0, 1.0 - ((double)multiTaskWeeks / state.PersonWeekGrid.Count));
    //     */
    // }
    public double GetAverageProjectsPerActivePersonWeek(ScheduleState state)
    {
        if (state.PersonWeekGrid.Count == 0) return 1.0;

        var projectsPerPersonWeek = new Dictionary<ScheduleState.PersonWeekKey, int>();

        foreach (var cell in state.PersonWeekGrid)
        {
            if (cell.Value <= 0) continue;

            var key = new ScheduleState.PersonWeekKey(cell.Key.PersonId, cell.Key.Week);

            if (!projectsPerPersonWeek.ContainsKey(key))
                projectsPerPersonWeek[key] = 0;

            projectsPerPersonWeek[key] += 1;
        }

        if (projectsPerPersonWeek.Count == 0) return 1.0;

        return projectsPerPersonWeek.Values.Average();
    }

    public double GetFocusScore(ScheduleState state)
    {
        // Raw metric user asked for:
        // avg projects per active person-week
        double avgProjects = GetAverageProjectsPerActivePersonWeek(state);

        // Convert to score in [0,1], where 1.0 is best
        return 1.0 / Math.Max(1.0, avgProjects);
    }


    public double GetContinuityScore(ScheduleState state)
    {
        if (state.Projects.Count == 0) return 1.0;

        double totalScore = 0;

        foreach (var project in state.Projects)
        {
            int originalCount = project.originalPeopleIds.Count;

            // If no baseline team exists, guess treat it as perfect for this project???
            if (originalCount == 0)
            {
                totalScore += 1.0;
                continue;
            }

            int overlap = 0;
            foreach (var person in project.people)
            {
                // Count how many current people were in the original team
                if (project.originalPeopleIds.Contains(person.id))
                    overlap++;
            }

            // Score is the share of original people still on the project.
            double projectScore = (double)overlap / originalCount;
            totalScore += projectScore;
        }

        // Average continuity across all projects.
        return totalScore / state.Projects.Count;
    }


    public double GetDurationScore(ScheduleState state)
    {
        double totalScore = 0;
        foreach (var project in state.Projects)
        {
            // Get the current shift (offset) applied to this project by the algorithm
            int currentShift = state.GetShift(project);
            // Retrieve all occupied cells (person-weeks) for this project at its current position
            var projectCells = state.GetGrid(project, currentShift);

            if (!projectCells.Any()) continue;
            // Calculate the actual timeline span by finding the earliest start and latest end across all team members assigned to this project.
            int actualStart = projectCells.Min(c => c.Week);
            int actualEnd = projectCells.Max(c => c.Week);
            int actualSpan = (actualEnd - actualStart) + 1;
            // Determine the original planned duration as the socre for efficiency
            int plannedSpan = project.OriginalDurationSpan;
            if (plannedSpan <= 0)
            {
                plannedSpan = actualSpan;
            }
            // A score of 1.0 means the project is perfectly compact.
            double score = (double)Math.Min(plannedSpan, actualSpan) / Math.Max(plannedSpan, actualSpan);
            totalScore += Math.Min(1.0, score);
        }
        return state.Projects.Count > 0 ? totalScore / state.Projects.Count : 1.0;
    }


    //----------------------------------------
    public ScheduleState GetCurrentState() => _state;
    public AvailabilityFinder GetFinder() => _finder;




    // evaluate move socre/delta in order to allow alg evaluate
    public double EvaluateMoveDelta(Project p, int candidateShift)
    {
        double scoreBefore = CalculateFitnessScore(_state);

        int originalShift = _state.GetShift(p);
        _state.ApplyShift(p, candidateShift); // simulate shift

        double scoreAfter = CalculateFitnessScore(_state);

        _state.ApplyShift(p, originalShift); // 

        return scoreAfter - scoreBefore; // return delta
    }



    // Compares the overall fitness improvement (Delta) to find the optimal project timing.
    public int GetBestMoveForProject(Project p)
    {
        int bestShift = _state.GetShift(p);
        double maxDelta = -999;   //Initial baseline: start with the project's current position

        // try valid shifts
        foreach (int shift in _state.GetValidShifts(p)) //Looping to compare each delta
        {
            double delta = EvaluateMoveDelta(p, shift);//Evaluation using Delta
            if (delta > maxDelta)
            {
                maxDelta = delta;
                bestShift = shift;
            }
        }
        return bestShift; // Return the shift that balances all 5 weighted criteria most effectively.
    }


    // Performs a "vertical" resource optimization by searching for the best qualified 
    // candidate to replace a current team member without changing the project timeline.
    public Person DetermineBestReplacement(Project project, Person currentPerson)
    {
        // Identify all eligible candidates who possess the same professional Role.
        var candidates = new List<Person>();
        foreach (Project p in _state.Projects)
        {
            foreach (Person person in p.people)
            {
                if (person.role.Equals(currentPerson.role))
                {
                    candidates.Add(person);
                }
            }
        }

        Person bestCandidate = currentPerson;

        //  Establish the baseline score to compare against during simulation.
        double maxScore = CalculateFitnessScore(_state);

        foreach (var candidate in candidates)
        {
            // Optimization: Skip evaluation if the candidate is already assigned.
            if (candidate.id == currentPerson.id) continue;

            // Simulation: Tentatively swap the resource in the project to test the impact.
            _state.SwapPersonInProject(project, currentPerson, candidate);

            // Assess how this specific swap affects the global Fitness Score.
            // It primarily impacts Focus Score (20%) and Conflict Score (40%).
            double newScore = CalculateFitnessScore(_state);

            // Comparison: Update the "bestCandidate" if this resource swap improves overall schedule health.
            if (newScore > maxScore)
            {
                maxScore = newScore;
                bestCandidate = candidate;
            }

            // Rollback: Revert the project assignment to its original state for the next iteration.
            _state.SwapPersonInProject(project, candidate, currentPerson);
        }

        // Return the candidate that yields the highest efficiency without changing the project timeline.
        return bestCandidate;
    }




    //--------------evaluate if add a new project what is the ideal time and optimal resource------
    public double EvaluateNewProjectInsertion(Project newProject)
    {
        // record the socre when if without new project 
        double originalScore = CalculateFitnessScore(_state);

        // find the ideal time windo
        int bestShift = GetBestMoveForProject(newProject);
        _state.ApplyShift(newProject, bestShift);

        // find the ideal huamn resouce
        foreach (var person in newProject.people.ToList())
        {
            Person betterStaff = DetermineBestReplacement(newProject, person); //The DetermineBestReplacement method automatically filters candidates 
                                                                               // based on role and selects the one with the highest Fitness Score.
            if (betterStaff.id != person.id)
            {
                _state.SwapPersonInProject(newProject, person, betterStaff);
            }
        }

        // evaluate if insert new project , what socre of it
        double newScore = CalculateFitnessScore(_state);

        // returen delta socre to evaluate does insert successs, if score is positive it present success
        return newScore - originalScore;
    }


    // Returns a comprehensive "Search Map" of how moving this project (p) 
    // to different weeks impacts the global schedule's Fitness Score.
    // This might be useful for the Analyzer to determine if a project is "un-placeable" 
    // due to time constraints or resource shortages.
    public List<ShiftPerformance> GetScoreTrendForProject(Project p)
    {
        var trend = new List<ShiftPerformance>();
        int originalShift = _state.GetShift(p);
        var options = _state.GetValidShifts(p);

        foreach (int shift in options)
        {
            _state.ApplyShift(p, shift);
            double totalScore = CalculateFitnessScore(_state);

            trend.Add(new ShiftPerformance
            {
                Shift = shift,
                Score = totalScore,
                IsOptimal = false
            });

            _state.ApplyShift(p, originalShift); // roll back
        }
        return trend.OrderByDescending(t => t.Score).ToList();
    }



    // a method to help adding new projetc since we need to find optimal resouce with same role
    public Dictionary<string, RoleGapReport> GetRoleSaturation(int startWeek, int endWeek)
    {
        var report = new Dictionary<string, RoleGapReport>();
        var allRoles = _state.People.Select(p => p.role).Distinct();

        foreach (var role in allRoles)
        {
            int supplyWeeks = _state.People.Count(p => p.role == role) * (endWeek - startWeek + 1);

            int demandWeeks = 0;
            foreach (var proj in _state.Projects)
            {
                var shift = _state.GetShift(proj);
                var cells = _state.GetGrid(proj, shift)
                                 .Where(c => c.Week >= startWeek && c.Week <= endWeek);

                foreach (var cell in cells)
                {
                    var person = _state.People.First(p => p.id == cell.PersonId);
                    if (person.role == role) demandWeeks++;
                }
            }

            double saturation = supplyWeeks == 0 ? 0 : (double)demandWeeks / supplyWeeks;

            report[role] = new RoleGapReport
            {
                Saturation = saturation,
                MissingHours = (demandWeeks > supplyWeeks) ? (demandWeeks - supplyWeeks) * 40 : 0,
                RecommendedStaff = Math.Max(0, Math.Ceiling((double)(demandWeeks - supplyWeeks) / (endWeek - startWeek + 1)))
            };
        }
        return report;
    }



    //----return conflicts detail: exist conflict in which project whose overloap and which week overloap
    // public List<string> GetDetailedConflictList()
    // {
    //     var details = new List<string>();

    //     foreach (var entry in _state.PersonWeekGrid.Where(kv => kv.Value >= 2))
    //     {
    //         var key = entry.Key;
    //         var person = _state.People.First(p => p.id == key.PersonId);

    //         var conflictingProjects = _state.Projects
    //             .Where(proj => _state.GetGrid(proj, _state.GetShift(proj))
    //             .Any(cell => cell.PersonId == key.PersonId && cell.Week == key.Week))
    //             .Select(proj => proj.name)
    //             .ToList();

    //         details.Add($"Week {key.Week} | {person.name} | Projects: {string.Join(" & ", conflictingProjects)}");
    //     }
    //     return details;
    // }



    private string FormatWeeksIntoRanges(List<int> weeks)
    {
        if (weeks == null || !weeks.Any()) return "None";
        var sortedWeeks = weeks.Distinct().OrderBy(w => w).ToList();

        var ranges = new List<string>();
        int start = sortedWeeks[0];
        int end = sortedWeeks[0];

        for (int i = 1; i < sortedWeeks.Count; i++)
        {
            if (sortedWeeks[i] == end + 1)
            {
                end = sortedWeeks[i];
            }
            else
            {
                ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
                start = end = sortedWeeks[i];
            }
        }
        ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
        return string.Join(", ", ranges);
    }




    public ScheduleState Finalize()
    {
        return _state;
    }
}


public class ShiftScore
{
    public int DeltaDoubleBooked { get; set; }
    public int OverlapAfter { get; set; }
    public int ShiftDistance { get; set; }
    public double Fitness { get; set; }

}


