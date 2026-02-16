using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OllamaSpike
{
    public class OpenAITest
    {
        public static async Task TestOpenAIExplanation(string apiKey)
        {
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("OPENAI GPT-4o-mini EXPLANATION TEST");
            Console.WriteLine(new string('=', 70) + "\n");

            try
            {
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

                var beforeState = new ScheduleState(people, projects);
                var handlerBefore = new ScheduleHandler(beforeState);

                var greedy = new GreedyAlg();
                var afterState = greedy.StartGreedy(people, projects);

                var handlerAfter = new ScheduleHandler(afterState);
                
                Console.WriteLine($"BEFORE: Fitness={handlerBefore.CalculateFitnessScore(beforeState):F2}");
                Console.WriteLine($"AFTER:  Fitness={handlerAfter.CalculateFitnessScore(afterState):F2}");

                // Generate changes
                List<string> actualChanges = GenerateChangesList(beforeState, afterState);

                Console.WriteLine("\nCalling OpenAI GPT-4o-mini...\n");
                
                var explainer = new ScheduleExplainerOpenAI(apiKey, "gpt-4o-mini");
                
                string explanation = await explainer.ExplainOptimization(
                    beforeState,
                    afterState,
                    handlerAfter,
                    actualChanges
                );

                Console.WriteLine("=== OPENAI EXPLANATION ===");
                Console.WriteLine(explanation);
                Console.WriteLine("\n✓ OpenAI test completed!\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ OpenAI test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static List<string> GenerateChangesList(ScheduleState before, ScheduleState after)
        {
            var changes = new List<string>();

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

            int beforeOverloads = before.PersonWeekHours.Count(kv => kv.Value > 40);
            int afterOverloads = after.PersonWeekHours.Count(kv => kv.Value > 40);
            
            if (beforeOverloads != afterOverloads)
            {
                changes.Add($"Reduced overloaded person-weeks from {beforeOverloads} to {afterOverloads}");
            }

            if (changes.Count == 0)
            {
                changes.Add("No changes made - schedule already optimal");
            }

            return changes;
        }
    }
}