using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace OllamaSpike;

public class CSVScheduleTest
{
  public static async Task TestScheduleOptimizationExplanation(OllamaApiClient ollama)
  {
    Console.WriteLine(" CSV Schedule: Explain Optimization Changes");
    Console.WriteLine(new string('*', 70));

    string prompt = @"
You are a schedule optimization assistant. Analyze what changed and explain WHY based on the fitness rules.

CAPACITY: Each person has 40 hours/week maximum

FITNESS SCORE RULES (weighted):
- Conflict Score (40%): Penalize overallocation (person assigned >40h/week)
- Movement Score (20%): Penalize people switching between projects frequently
- Focus Score (20%): Reward people working on fewer projects simultaneously
- Continuity Score (10%): Reward longer consecutive weeks on same project
- Duration Score (10%): Penalize very short assignments

ORIGINAL SCHEDULE:
Person      | Project    | W3  | W4  | W5  | W6  | W7
Person_14   | Project_03 | 40  | 30  | 40  | 40  | 40   (Total: 190h / 5 weeks)
Person_07   | Project_03 | 40  | 40  | 40  | 40  | 40   (Total: 200h / 5 weeks)
Person_24   | Project_03 | 35  | 25  | 40  | 40  | 40   (Total: 180h / 5 weeks)

OPTIMIZED SCHEDULE:
Person      | Project    | W3  | W4  | W5  | W6  | W7
Person_14   | Project_03 | 40  | 40  | 40  | 40  | 40   (Total: 200h / 5 weeks) 
Person_07   | Project_03 | 40  | 40  | 40  | 40  | 40   (Total: 200h / 5 weeks)
Person_24   | Project_03 | 30  | 20  | 40  | 40  | 40   (Total: 170h / 5 weeks)

CHANGES DETECTED:
1. Person_14, W4: 30h → 40h (+10h)
2. Person_24, W3: 35h → 30h (-5h)
3. Person_24, W4: 25h → 20h (-5h)

Explain in 3-4 sentences:
1. What changed?
2. Why did the optimizer make these changes?
3. Which fitness rules drove these decisions?
";

    try
    {
      var chat = new Chat(ollama);
      string response = "";

      Console.WriteLine("Asking LLM to explain optimization...\n");

      await foreach (var token in chat.SendAsync(prompt))
      {
        response += token;
      }

      Console.WriteLine($"LLM Explanation:\n{response}");
    }
    catch (Exception ex)
    {
      Console.WriteLine($" Error: {ex.Message}");
    }

    Console.WriteLine("\n" + new string('=', 70));
  }
}