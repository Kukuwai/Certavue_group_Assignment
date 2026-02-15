using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OllamaSharp;
using OllamaSharp.Models.Chat;

namespace OllamaSpike
{
  /// <summary>
  /// Schedule explainer using OllamaSharp.
  /// Works with OllamaSharp versions where ChatAsync returns IAsyncEnumerable (streamed chunks),
  /// which is why we must use "await foreach" (not "await").
  /// </summary>
  public class ScheduleExplainerOllama
  {
    private readonly OllamaApiClient _ollama;
    private readonly string _modelId;

    public ScheduleExplainerOllama(OllamaApiClient ollama, string modelId = "llama3.2:3b")
    {
      _ollama = ollama ?? throw new ArgumentNullException(nameof(ollama));
      _modelId = modelId;
      _ollama.SelectedModel = _modelId;
    }

    /// <summary>
    /// Returns a full explanation string by collecting streamed chunks from ChatAsync.
    /// </summary>
    public async Task<string> ExplainOptimization(
        ScheduleState beforeState,
        ScheduleState afterState,
        ScheduleHandler handler,
        List<string> changesLog)
    {
      if (beforeState == null) throw new ArgumentNullException(nameof(beforeState));
      if (afterState == null) throw new ArgumentNullException(nameof(afterState));
      if (handler == null) throw new ArgumentNullException(nameof(handler));

      // This method below is used here to build the promt that will be passed to the LLM.
      string prompt = BuildPrompt(beforeState, afterState, handler, changesLog);

      var request = new ChatRequest
      {
        Model = _modelId,
        Messages = new List<Message>
        {
          new Message { Role = "user", Content = prompt }
        }
      };

      var sb = new StringBuilder();

      await foreach (var chunk in _ollama.ChatAsync(request))
      {
        var text = chunk?.Message?.Content;
        if (!string.IsNullOrEmpty(text))
          sb.Append(text);
      }

      return sb.ToString();
    }

    // This is the actual method that creates the promt.
    private string BuildPrompt(
        ScheduleState before,
        ScheduleState after,
        ScheduleHandler handler,
        List<string> changes)
    {
      var sb = new StringBuilder();

      sb.AppendLine("You are a schedule optimization expert. Explain what changed and why.");
      sb.AppendLine("Write in plain english language.");
      sb.AppendLine();
      sb.AppendLine("=== FITNESS SCORING RULES ===");
      sb.AppendLine("- Conflict Score (40%): People working >40 hours/week is bad");
      sb.AppendLine("- Movement Score (20%): Shifting projects far from original timeline is bad");
      sb.AppendLine("- Focus Score (20%): People juggling many projects is bad");
      sb.AppendLine("- Continuity Score (10%): Changing team members is bad");
      sb.AppendLine("- Duration Score (10%): Stretching project timelines is bad");

      // The GetScoreBreakDown method is defined below. It essentially gives dictionry with string as key and scores as values. 
      var beforeScores = GetScoreBreakdown(before, handler);
      var afterScores = GetScoreBreakdown(after, handler);

      sb.AppendLine();
      sb.AppendLine("=== BEFORE OPTIMIZATION ===");
      sb.AppendLine($"Total Score: {beforeScores["Total"]:F2}");
      sb.AppendLine($"  - Conflict: {beforeScores["Conflict"]:F2} (40% weight)");
      sb.AppendLine($"  - Movement: {beforeScores["Movement"]:F2} (20% weight)");
      sb.AppendLine($"  - Focus: {beforeScores["Focus"]:F2} (20% weight)");
      sb.AppendLine($"  - Continuity: {beforeScores["Continuity"]:F2} (10% weight)");
      sb.AppendLine($"  - Duration: {beforeScores["Duration"]:F2} (10% weight)");

      sb.AppendLine();
      sb.AppendLine("=== AFTER OPTIMIZATION ===");
      sb.AppendLine($"Total Score: {afterScores["Total"]:F2}");
      sb.AppendLine($"  - Conflict: {afterScores["Conflict"]:F2}");
      sb.AppendLine($"  - Movement: {afterScores["Movement"]:F2}");
      sb.AppendLine($"  - Focus: {afterScores["Focus"]:F2}");
      sb.AppendLine($"  - Continuity: {afterScores["Continuity"]:F2}");
      sb.AppendLine($"  - Duration: {afterScores["Duration"]:F2}");

      sb.AppendLine();
      sb.AppendLine("=== CHANGES MADE ===");
      if (changes != null && changes.Count > 0)
      {
        foreach (var change in changes)
          sb.AppendLine($"- {change}");
      }
      else
      {
        sb.AppendLine("- No changes were needed (schedule was already optimal)");
      }

      // avoid divide-by-zero
      double beforeTotal = beforeScores["Total"];
      double improvement = afterScores["Total"] - beforeTotal;

      sb.AppendLine();
      if (Math.Abs(beforeTotal) < 1e-9)
      {
        sb.AppendLine($"=== OVERALL IMPROVEMENT: {improvement:+0.00;-0.00} (percentage: n/a) ===");
      }
      else
      {
        sb.AppendLine(
          $"=== OVERALL IMPROVEMENT: {improvement:+0.00;-0.00} ({improvement / beforeTotal * 100:+0.0;-0.0}%) ===");
      }

      // Helps reduce hallucinations
      sb.AppendLine();
      sb.AppendLine("Important:");
      sb.AppendLine("- Only describe changes that appear in CHANGES MADE and the score differences shown above.");
      sb.AppendLine("- If exact schedule moves are not listed, say you cannot infer exact moves.");

      sb.AppendLine();
      sb.AppendLine("=== YOUR TASK ===");
      sb.AppendLine("Explain in 2-3 short paragraphs:");
      sb.AppendLine("1. What were the main problems before (reference specific scores)");
      sb.AppendLine("2. What specific changes were made (from CHANGES MADE)");
      sb.AppendLine("3. Why these changes improved the score (which components got better and why)");

      return sb.ToString();
    }

    private Dictionary<string, double> GetScoreBreakdown(ScheduleState state, ScheduleHandler handler)
    {
      return new Dictionary<string, double>
      {
        ["Total"] = handler.CalculateFitnessScore(state),
        ["Conflict"] = handler.GetConflictScore(state),
        ["Movement"] = handler.GetMovementScore(state),
        ["Focus"] = handler.GetFocusScore(state),
        ["Continuity"] = handler.GetContinuityScore(state),
        ["Duration"] = handler.GetDurationScore(state)
      };
    }
  }
}
