using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.OrTools.PDLP;

public class ScheduleHandler
{
    private readonly ScheduleState _state;

    public ScheduleHandler(ScheduleState state)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
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


/// <summary>
/// Evaluates schedule stability by measuring project displacement (Shifts).
/// Penalties are normalized by the maximum allowed movement and weighted by project size.
/// 1.0 = No movement (Perfect); 0.0 = All projects moved to their maximum limit.
/// </summary>
    public double GetMovementScore(ScheduleState state)
    {
        // If no projects exist, the schedule is technically "undisturbed" (Perfect 1.0)
        if (state.Projects.Count == 0) return 1.0;

        double weightedPenaltySum = 0.0;
        double weightSum = 0.0;

        foreach (Project project in state.Projects)
        {
            // Determine the boundaries of possible movement for this project
            List<int> validShifts = state.GetValidShifts(project);
            // Find the maximum potential displacement to use as a normalization baseline
            int maxAllowedShift = validShifts.Count == 0 ? 0 : validShifts.Max(s => Math.Abs(s));
            // Get the absolute value of the project's current displacement
            int currentShift = Math.Abs(state.GetShift(project));
            //Normalize the shift into a 0.0 to 1.0 range (Distance Moved / Max Possible Distance)
            double normalizedShift = maxAllowedShift == 0
                ? 0.0
                : Math.Min(1.0, (double)currentShift / maxAllowedShift);
            // Weight the penalty by the project size so large projects have more impact
            double weight = GetProjectEffortHours(project);
            weightedPenaltySum += normalizedShift * weight;
            weightSum += weight;
        }
        // Safeguard against division by zero
        if (weightSum <= 0) return 1.0;
        // Convert the average penalty into a "Stability Score" (1.0 - Penalty)
        double averagePenalty = weightedPenaltySum / weightSum;
        return Math.Max(0.0, 1.0 - averagePenalty);
    }


/// Calculates the total labor hours assigned to a project.
/// Used as a weighting factor so that larger projects have a bigger impact on global scores.
    private static double GetProjectEffortHours(Project project)
    {
        double total = 0.0;
        // Iterate through every person currently assigned to the project
        foreach (Person person in project.people)
        { 
            // Check if the person has a task assignment record for this specific project
            if (!person.projects.TryGetValue(project, out Dictionary<int, int> weekHours))
                continue;
            // Sum up all hours assigned in each week where the value is greater than zero
            total += weekHours.Values.Where(h => h > 0).Sum();
        }
        // Return the total effort; defaults to 1.0 to avoid zero-weighting in score calculations
        return total > 0 ? total : 1.0;
    }


/// Calculates the average number of concurrent projects per person, per week.
/// Used to monitor multitasking overhead; higher values indicate fragmented focus for team members.
    public double GetAverageProjectsPerActivePersonWeek(ScheduleState state)
    {   // Return perfect score if no assignments exist
        if (state.PersonWeekGrid.Count == 0) return 1.0;

        // Map to track the count of unique projects assigned to (Person, Week) pairs
        var projectsPerPersonWeek = new Dictionary<ScheduleState.PersonWeekKey, int>();

        foreach (var cell in state.PersonWeekGrid)
        {   
            // Skip records where no hours are actually assigned
            if (cell.Value <= 0) continue;
            // Use a composite key of PersonId and Week to identify a specific "work week"
            var key = new ScheduleState.PersonWeekKey(cell.Key.PersonId, cell.Key.Week);
           // Increment the project count for this person in this specific week
            if (!projectsPerPersonWeek.ContainsKey(key))
                projectsPerPersonWeek[key] = 0;

            projectsPerPersonWeek[key] += 1;
        }
        // If no active work weeks were found, return 1.0
        if (projectsPerPersonWeek.Count == 0) return 1.0;
       // Return the statistical average of project counts per active work week
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


/// Measures team retention by comparing the current staff against the original team baseline.
/// Returns the percentage of original members still assigned (0.0 to 1.0).
/// High scores indicate minimal disruption to the project's institutional knowledge.

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

/// <summary>
/// Evaluates the Project Compactness Score.
/// This metric encourages the solver to keep project timelines within or shorter than their original planned duration.
/// </summary>
    public double GetDurationScore(ScheduleState state)
    {
        double totalScore = 0;
        foreach (var project in state.Projects)
        {
            // 1. Locate the project's current position on the timeline
            int currentShift = state.GetShift(project);
            // Retrieve all occupied cells (person-weeks) for this project at its current position
            var projectCells = state.GetGrid(project, currentShift);

            if (!projectCells.Any()) continue;
            // 2. Calculate the physical footprint (Actual Span) from start to end
            int actualStart = projectCells.Min(c => c.Week);
            int actualEnd = projectCells.Max(c => c.Week);
            int actualSpan = (actualEnd - actualStart) + 1;
            // 3. Compare against original plan
            int plannedSpan = project.OriginalDurationSpan;
            if (plannedSpan <= 0)
            {
                plannedSpan = actualSpan;
            }
            // 4. Calculate ratio: Only penalizes "stretching". 
            // If actualSpan is smaller than plannedSpan, the score remains 1.0.
            double score = (double)plannedSpan / Math.Max(plannedSpan, actualSpan);
            totalScore += Math.Min(1.0, score);
        }
        // 5. Return the average health score across all projects
        return state.Projects.Count > 0 ? totalScore / state.Projects.Count : 1.0;
    }


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


}




