using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;

public class OpenAI
{
    private HttpClient client;
    private string model;

    public OpenAI(string apiKey, string modelName)
    {
        model = modelName; //Probably 5.1 mini or nano
        client = new HttpClient();
        client.BaseAddress = new Uri("https://api.openai.com/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.Timeout = TimeSpan.FromMinutes(5); //request timeout time
    }

    public string GetModel()
    {
        return model;
    }

    public string CompareTwoCsvWithInstructions(string originalCsvPath, string updatedCsvPath, string instructionsTxtPath)
    {
        string instructions = File.ReadAllText(instructionsTxtPath);

        string originalFileId = UploadCsvAndGetFileId(originalCsvPath);
        string changedFileId = UploadCsvAndGetFileId(updatedCsvPath);

        return SendPromptWithTwoFiles(instructions, originalFileId, changedFileId, Path.GetFileName(originalCsvPath), Path.GetFileName(updatedCsvPath));
    }


    private string UploadCsvAndGetFileId(string csvPath)
    {
        using MultipartFormDataContent form = new MultipartFormDataContent();
        form.Add(new StringContent("user_data"), "purpose");

        byte[] bytes = File.ReadAllBytes(csvPath); //reads the CSV bytes
        ByteArrayContent fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv"); //notes it as CSV fomat
        form.Add(fileContent, "file", Path.GetFileName(csvPath)); //adds file

        HttpResponseMessage response = client.PostAsync("files", form).GetAwaiter().GetResult(); //upload file
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); 

        if (!response.IsSuccessStatusCode)
            throw new Exception("File upload failed: " + body); //stop if failed

        using JsonDocument doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("id", out JsonElement idElement))
            throw new Exception("Upload response missing file id: " + body);

        return idElement.GetString() ?? throw new Exception("Upload returned null file id.");
    }


    private string SendPromptWithTwoFiles(string instructions, string fileId1, string fileId2, string originalFileName, string updatedFileName)
{
    string comparePrompt =
        "Use code interpreter to load and compare the two CSV files.\n" +
        $"Original file: {originalFileName}\n" +
        $"Updated file: {updatedFileName}\n" +
        "Treat the first as original and second as updated.";

    var payload = new
    {
        model = model,
        instructions = instructions,
        background = true, //async and background processing
        tool_choice = "required", //makes it use tools
        tools = new object[]
        {
            new
            {
                type = "code_interpreter",
                container = new
                {
                    type = "auto",
                    file_ids = new[] { fileId1, fileId2 } //atachaed both files
                }
            }
        },
        input = comparePrompt
    };

    string json = JsonSerializer.Serialize(payload); //convert to json
    StringContent content = new StringContent(json, Encoding.UTF8, "application/json");

    HttpResponseMessage response = client.PostAsync("responses", content).GetAwaiter().GetResult(); //response job
    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

    if (!response.IsSuccessStatusCode)
        return body;

    using JsonDocument doc = JsonDocument.Parse(body);
    string responseId = GetOptionalStringProperty(doc.RootElement, "id");
    string status = GetOptionalStringProperty(doc.RootElement, "status");

    if (status == "completed")
    {
        return ExtractOutputText(body); //return text
    }

    if (string.IsNullOrWhiteSpace(responseId))
    {
        return body;
    }

    return WaitForCompletion(responseId, TimeSpan.FromMinutes(10), 2000); //timeout for each task 
}


    private string WaitForCompletion(string responseId, TimeSpan maxWait, int pollIntervalMs)
    {
        DateTime deadline = DateTime.UtcNow.Add(maxWait);

        while (DateTime.UtcNow < deadline)
        {
            HttpResponseMessage pollResponse = client.GetAsync("responses/" + responseId).GetAwaiter().GetResult();
            string pollBody = pollResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!pollResponse.IsSuccessStatusCode)
            {
                return pollBody;
            }

            using JsonDocument pollDoc = JsonDocument.Parse(pollBody);
            string status = GetOptionalStringProperty(pollDoc.RootElement, "status");

            if (status == "completed")
            {
                return ExtractOutputText(pollBody);
            }

            if (status == "failed" || status == "cancelled" || status == "incomplete")
            {
                return pollBody;
            }

            Thread.Sleep(pollIntervalMs);
        }

        return "{ \"error\": { \"message\": \"Timed out waiting for OpenAI response completion.\", \"type\": \"timeout_error\" } }";
    }

    private static string GetOptionalStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out JsonElement propertyElement) &&
            propertyElement.ValueKind == JsonValueKind.String)
        {
            return propertyElement.GetString() ?? string.Empty;
        }

        return string.Empty;
    }


    private string ExtractOutputText(string responseJson)
    {
        using JsonDocument doc = JsonDocument.Parse(responseJson);

        if (doc.RootElement.TryGetProperty("output_text", out JsonElement outputTextElement))
        {
            if (outputTextElement.ValueKind == JsonValueKind.String)
            {
                string text = outputTextElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }

            if (outputTextElement.ValueKind == JsonValueKind.Array)
            {
                StringBuilder topLevelText = new StringBuilder();
                foreach (JsonElement part in outputTextElement.EnumerateArray())
                {
                    if (part.ValueKind == JsonValueKind.String)
                    {
                        string segment = part.GetString();
                        if (!string.IsNullOrWhiteSpace(segment))
                        {
                            if (topLevelText.Length > 0) topLevelText.AppendLine().AppendLine();
                            topLevelText.Append(segment);
                        }
                    }
                }

                if (topLevelText.Length > 0)
                {
                    return topLevelText.ToString();
                }
            }
        }

        if (doc.RootElement.TryGetProperty("output", out JsonElement outputArray) &&
            outputArray.ValueKind == JsonValueKind.Array)
        {
            StringBuilder combined = new StringBuilder();

            foreach (JsonElement outputItem in outputArray.EnumerateArray())
            {
                if (!outputItem.TryGetProperty("type", out JsonElement itemType) ||
                    itemType.ValueKind != JsonValueKind.String ||
                    itemType.GetString() != "message")
                {
                    continue;
                }

                if (!outputItem.TryGetProperty("content", out JsonElement contentArray) ||
                    contentArray.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (JsonElement contentItem in contentArray.EnumerateArray())
                {
                    if (!contentItem.TryGetProperty("type", out JsonElement contentType) ||
                        contentType.ValueKind != JsonValueKind.String ||
                        contentType.GetString() != "output_text")
                    {
                        continue;
                    }

                    if (!contentItem.TryGetProperty("text", out JsonElement textElement) ||
                        textElement.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    string text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (combined.Length > 0) combined.AppendLine().AppendLine();
                        combined.Append(text);
                    }
                }
            }

            if (combined.Length > 0)
            {
                return combined.ToString();
            }
        }

        return responseJson;
    }
    public void Close()
    {
        if (client != null)
        {
            client.Dispose();
        }
    }
}
