using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OllamaSharp;

namespace OllamaSpike
{
  public class GreedyExplanationTest
  {
    public static async Task TestGreedyWithExplanation(OllamaApiClient ollama)
    {
      Console.WriteLine("\n" + new string('=', 70));
      Console.WriteLine("GREEDY ALGORITHM + OLLAMA EXPLANATION TEST");
      Console.WriteLine(new string('=', 70) + "\n");


      try
      {
        // Load real data
        var loader = new Loader();
        string testCsv = @"Person,Project,Role,w1,w2,w3,w4,w5,w6,w7,w8,w9,w10,w11,w12,w13,w14,w15,w16
                            Alice,ProjectA,Developer,s,0,0,30,30,30,30,10,10,10,10,0,0,0,e
                            Alice,ProjectB,Developer,s,0,30,30,30,30,10,10,10,10,0,0,0,0,e
                            Bob,ProjectA,Developer,s,0,0,10,10,10,10,30,30,30,30,0,0,0,e
                            Charlie,ProjectB,Developer,s,0,30,30,30,30,30,30,30,30,0,0,0,0,e";

        string tempFile = System.IO.Path.GetTempFileName();
        System.IO.File.WriteAllText(tempFile, testCsv);
        var (people, projects) = loader.LoadData(tempFile);
        System.IO.File.Delete(tempFile);

        // BEFORE optimization
        var beforeState = new ScheduleState(people, projects);
        var handlerBefore = new ScheduleHandler(beforeState);

        Console.WriteLine("BEFORE Optimization:");
        Console.WriteLine($"  Fitness Score: {handlerBefore.CalculateFitnessScore(beforeState):F2}");
        Console.WriteLine($"  Conflict Score: {handlerBefore.GetConflictScore(beforeState):F2}");

        // RUN Greedy Algorithm
        Console.WriteLine("\nRunning Greedy Algorithm...");

        // Check for overloads before
        Console.WriteLine("\nChecking for overloads before optimization:");
        foreach (var kv in beforeState.PersonWeekHours)
        {
          if (kv.Value > 40)
          {
            Console.WriteLine($"  Person {kv.Key.PersonId}, Week {kv.Key.Week}: {kv.Value} hours (OVERLOADED by {kv.Value - 40})");
          }
        }

        var greedy = new GreedyAlg();
        var afterState = greedy.StartGreedy(people, projects);

        // Check for overloads after
        Console.WriteLine("\nChecking for overloads after optimization:");
        foreach (var kv in afterState.PersonWeekHours)
        {
          if (kv.Value > 40)
          {
            Console.WriteLine($"  Person {kv.Key.PersonId}, Week {kv.Key.Week}: {kv.Value} hours (STILL OVERLOADED by {kv.Value - 40})");
          }
        }

        // After optimization
        var handlerAfter = new ScheduleHandler(afterState);
        Console.WriteLine("\nAFTER Optimization:");
        Console.WriteLine($"  Fitness Score: {handlerAfter.CalculateFitnessScore(afterState):F2}");
        Console.WriteLine($"  Conflict Score: {handlerAfter.GetConflictScore(afterState):F2}");

        // Generate explanation with Ollama
        Console.WriteLine("\nGenerating explanation with Ollama...\n");

        // Generate actual changes list by comparing before/after
        List<string> actualChanges = GenerateChangesList(beforeState, afterState);

        Console.WriteLine("\n**** Here are the actual changes after Greedy Run ***");
        Console.WriteLine(string.Join("\n  - ", actualChanges));

        var explainer = new ScheduleExplainerOllama(ollama);

        string explanation = await explainer.ExplainOptimization(
            beforeState,
            afterState,
            handlerAfter,
            actualChanges
        );

        Console.WriteLine("=== OLLAMA EXPLANATION ===");
        Console.WriteLine(explanation);
        Console.WriteLine("\nTest completed successfully!\n");
      }
      catch (Exception ex)
      {
        Console.WriteLine($"Test failed: {ex.Message}");
        Console.WriteLine($"Stack trace: {ex.StackTrace}");
      }
    }

    // Method for generating changes. What changed between before and after running the Greedy.
    private static List<string> GenerateChangesList(ScheduleState before, ScheduleState after)
    {
      var changes = new List<string>();

      // Compare project shifts
      foreach (var project in after.Projects)
      {
        int beforeShift = before.GetShift(project);
        int afterShift = after.GetShift(project);

        if (beforeShift != afterShift)
        {
          int difference = afterShift - beforeShift;
          string direction = difference > 0 ? "forward" : "backward";
          changes.Add($"Shifted project '{project.name}' {Math.Abs(difference)} weeks {direction}");
        }
      }

      // Compare person-week overloads
      int beforeOverloads = 0;
      int afterOverloads = 0;

      foreach (var kv in before.PersonWeekHours)
      {
        if (kv.Value > 40) beforeOverloads++;
      }

      foreach (var kv in after.PersonWeekHours)
      {
        if (kv.Value > 40) afterOverloads++;
      }

      if (beforeOverloads != afterOverloads)
      {
        changes.Add($"Reduced overloaded person-weeks from {beforeOverloads} to {afterOverloads}");
      }

      // Compare team assignments
      foreach (var project in after.Projects)
      {
        var beforeProject = before.Projects.FirstOrDefault(p => p.id == project.id);
        if (beforeProject == null) continue;

        var beforePeople = beforeProject.people;
        var afterPeople = project.people;

        var added = afterPeople.Where(p => !beforePeople.Any(bp => bp.id == p.id)).ToList();
        var removed = beforePeople.Where(p => !afterPeople.Any(ap => ap.id == p.id)).ToList();

        foreach (var person in added)
        {
          changes.Add($"Added {person.name} ({person.role}) to project '{project.name}'");
        }

        foreach (var person in removed)
        {
          changes.Add($"Removed {person.name} ({person.role}) from project '{project.name}'");
        }
      }

      if (changes.Count == 0)
      {
        changes.Add("No changes made - schedule was already optimal or no valid improvements found");
      }

      return changes;
    }
  }
}