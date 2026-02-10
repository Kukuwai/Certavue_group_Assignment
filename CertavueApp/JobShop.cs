// using System;
// using System.Collections.Generic;
// using GeneticSharp.Domain.Chromosomes;
// using GeneticSharp.Domain.Fitnesses;


// public class ProjectSchedulPlan : GeneticSharp.Domain.Chromosomes.ChromosomeBase
// {
//     private readonly List<Project> _projects;
//     private readonly ScheduleState _state;


//     public ProjectSchedulPlan(List<Project> projects, ScheduleState state) : base(projects.Count)
//     {
//         _projects = projects;
//         _state = state;
//         CreateGenes(); 
//     }

//     public override Gene GenerateGene(int geneIndex)
//     {
//         var project = _projects[geneIndex];
//         var validShifts = _state.GetValidShifts(project);
//         var randomShift = validShifts[GeneticSharp.Domain.Randomizations.RandomizationProvider.Current.GetInt(0, validShifts.Count)];
//         return new Gene(randomShift);
//     }

//     public override IChromosome CreateNew()
//     {
//         return new ProjectSchedulPlan(_projects, _state);
//     }
// }

// public class ProjectFitness : IFitness 
// { 
//     private readonly ScheduleState _state;
//     private readonly List<Project> _projects;
//     private readonly ScheduleHandler _handler;


//     public ProjectFitness(ScheduleState state, List<Project> projects)
//     {
//         _state = state;
//         _projects = projects;
//         _handler = new ScheduleHandler(state); 
//     }

//     public double Evaluate(IChromosome chromosome)
//     {

//         var c = (ProjectSchedulPlan)chromosome;
        
//         List<int> appliedShifts = new List<int>();

//         for (int i = 0; i < _projects.Count; i++)
//         {
//             int shiftValue = (int)c.GetGene(i).Value;
//             _state.ApplyShift(_projects[i], shiftValue); 
//             appliedShifts.Add(shiftValue);
//         }

//         double score = _handler.CalculateFitnessScore(_state);

//         for (int i = 0; i < _projects.Count; i++)
//         {
//             _state.ApplyShift(_projects[i], -appliedShifts[i]); 
//         }

//         return score;
//     }
// }