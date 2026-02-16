using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class OpenAI
{
    private HttpClient client; 
    private string model;      

    public OpenAI(string apiKey, string modelName)
    {
        model = modelName; //Probably 5.1 mini
        client = new HttpClient(); 
        client.BaseAddress = new Uri("https://api.openai.com/v1/"); 
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey); 
    }

    public string GetModel()
    {
        return model; 
    }

    public string CompareTwoCsvWithInstructions(string originalCsvPath, string updatedCsvPath, string instructionsTxtPath)
    {
        string instructions = File.ReadAllText(instructionsTxtPath); //Instruction text to be read

        string OriginalFileId = UploadCsvAndGetFileId(originalCsvPath); //Original CSV
        string ChangedFileId = UploadCsvAndGetFileId(updatedCsvPath);  //After changes

        return SendPromptWithTwoFiles(instructions, OriginalFileId, ChangedFileId); 
    }

    private string UploadCsvAndGetFileId(string csvPath)
    {
        MultipartFormDataContent form = new MultipartFormDataContent(); //Multipart body file upload
        form.Add(new StringContent("user_data"), "purpose"); //Metadata

        byte[] bytes = File.ReadAllBytes(csvPath); //Reading the csv bytes 
        ByteArrayContent fileContent = new ByteArrayContent(bytes); //Wraps bytes
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv"); //MIME type set
        form.Add(fileContent, "file", Path.GetFileName(csvPath)); //Adds the file with name

        HttpResponseMessage response = client.PostAsync("files", form).GetAwaiter().GetResult(); //POST /files
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); //Reads JSON

        JsonDocument doc = JsonDocument.Parse(body); //Parsing JSON
        JsonElement idElement;
        bool hasId = doc.RootElement.TryGetProperty("id", out idElement); //Try to read upload

        string fileId = idElement.GetString(); 
        doc.Dispose(); //JSON document

        return fileId; 
    }

    private string SendPromptWithTwoFiles(string instructions, string fileId1, string fileId2)
    {
        string prompt = "\n" + instructions; // Final user prompt text

        // Build request payload manually as JSON string
        string json = "{" +"\"model\":\"" + EscapeJson(model) + "\"," + "\"input\":[" + "{" + "\"role\":\"user\"," + "\"content\":[" + "{" + "\"type\":\"input_text\"," + "\"text\":\"" + EscapeJson(prompt) + "\"" + "}," + "{" + "\"type\":\"input_file\"," + "\"file_id\":\"" + EscapeJson(fileId1) + "\"" + "}," + "{" + "\"type\":\"input_file\"," + "\"file_id\":\"" + EscapeJson(fileId2) + "\"" + "}" + "]" + "}" + "]" + "}";

        StringContent content = new StringContent(json, Encoding.UTF8, "application/json"); //HTTP body 
        HttpResponseMessage response = client.PostAsync("responses", content).GetAwaiter().GetResult(); //Response
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult(); //Reads the response

        return ExtractOutputText(body); //Returns only the output text
    }

    private string ExtractOutputText(string responseJson)
    {
        JsonDocument doc = JsonDocument.Parse(responseJson); //Parse response JSON
        JsonElement outputTextElement;
        bool hasOutputText = doc.RootElement.TryGetProperty("output_text", out outputTextElement); //Check top output

        if (hasOutputText && outputTextElement.ValueKind == JsonValueKind.String)
        {
            string text = outputTextElement.GetString(); //Pulls the text
            doc.Dispose(); //Releases JSON doc

            if (text != null && text.Length > 0)
            {
                return text; //Returns text when it appears
            }
        }
        else
        {
            doc.Dispose(); //JSON doc if no text is in it 
        }

        return responseJson; //Full JSON
    }

    private string EscapeJson(string text)
    {
        string result = text; //Work on a copy
        result = result.Replace("\\", "\\\\"); 
        result = result.Replace("\"", "\\\""); 
        result = result.Replace("\r", "\\r"); 
        result = result.Replace("\n", "\\n"); 
        return result; //JSON safe text
    }
}