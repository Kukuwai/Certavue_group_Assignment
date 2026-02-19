using OllamaSharp;
using OllamaSharp.Models.Chat;
using System.IO;
using System.Text;
using System.Threading.Tasks;

public class OllamaScheduleExplainer
{
  private OllamaApiClient ollama;

  public OllamaScheduleExplainer(string modelName = "llama3.2:3b")
  {
    ollama = new OllamaApiClient(new Uri("http://localhost:11434"))
    {
      SelectedModel = modelName
    };
  }

  public async Task<string> CompareTwoCsvWithInstructions(
      string originalCsvPath,
      string updatedCsvPath,
      string instructionsTxtPath)
  {
    // Read instruction file
    string instructions = File.ReadAllText(instructionsTxtPath); // I am using a smaller Instruction file with only essential questions as Ollama can not handle large prompts. 

    // Read CSV files (limit lines on CSV file to prevent huge prompts). 
    // Ollama on my laptop can not handle large prompts. It is resulting in time-out. 
    string originalCsv = ReadCsvSample(originalCsvPath, maxLines: 24); // With the modified Ollama instructions it is not working with more than 25 lines. 
    string updatedCsv = ReadCsvSample(updatedCsvPath, maxLines: 24);

    // Build prompt
    string prompt = BuildComparisonPrompt(instructions, originalCsv, updatedCsv);

    // Get explanation from Ollama
    try
    {
      var chat = new Chat(ollama);
      StringBuilder response = new StringBuilder();

      await foreach (var token in chat.SendAsync(prompt))
      {
        response.Append(token);
      }

      return response.ToString();
    }
    catch (Exception ex)
    {
      return $"Error: {ex.Message}\nMake sure Ollama is running.";
    }
  }

  private string ReadCsvSample(string csvPath, int maxLines)
  {
    var lines = File.ReadAllLines(csvPath);
    int linesToRead = Math.Min(maxLines, lines.Length);

    var sample = new StringBuilder();
    for (int i = 0; i < linesToRead; i++)
    {
      sample.AppendLine(lines[i]);
    }

    if (lines.Length > maxLines)
    {
      sample.AppendLine($"... ({lines.Length - maxLines} more rows)");
    }

    return sample.ToString();
  }

  private string BuildComparisonPrompt(string instructions, string originalCsv, string updatedCsv)
  {
    return $@"
You are a schedule optimization analyst. Compare the two CSV schedules and explain what changed and why.

INSTRUCTIONS/FITNESS RULES:
{instructions}

ORIGINAL SCHEDULE (before optimization):
{originalCsv}

OPTIMIZED SCHEDULE (after greedy algorithm):
{updatedCsv}

Follow the output format specified in the instructions above.
";
  }

  public void Close()
  {
  }
}