using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;


public class OpenAI
{
    private HttpClient client;
    private string model;

    public string SendPrompt(string prompt)
    {
        string json =
            "{"
            + "\"model\":\"" + model + "\","
            + "\"input\":["
                + "{"
                    + "\"role\":\"user\","
                    + "\"content\":["
                        + "{"
                            + "\"type\":\"input_text\","
                            + "\"text\":\"" + EscapeJson(prompt) + "\""
                        + "}"
                    + "]"
                + "}"
            + "]"
            + "}";

        StringContent content = new StringContent(json, Encoding.UTF8, "application/json");
        HttpResponseMessage response = client.PostAsync("responses", content).GetAwaiter().GetResult();
        string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

        if (response.IsSuccessStatusCode == false)
        {
            throw new Exception("OpenAI request failed: " + response.StatusCode + " | " + body);
        }

        JsonDocument doc = JsonDocument.Parse(body);
        JsonElement root = doc.RootElement;
        JsonElement outputTextElement;

        bool hasOutputText = root.TryGetProperty("output_text", out outputTextElement);
        if (hasOutputText && outputTextElement.ValueKind == JsonValueKind.String)
        {
            string text = outputTextElement.GetString();
            doc.Dispose();

            if (text != null && text.Length > 0)
            {
                return text;
            }
        }
        else
        {
            doc.Dispose();
        }

        return body;
    }

    private string EscapeJson(string text)
    {
        if (text == null)
        {
            return "";
        }

        string result = text;
        result = result.Replace("\\", "\\\\");
        result = result.Replace("\"", "\\\"");
        result = result.Replace("\r", "\\r");
        result = result.Replace("\n", "\\n");
        return result;
    }


    public OpenAI(string apiKey, string modelName)
    {
        client = new HttpClient();
        client.BaseAddress = new Uri("https://api.openai.com/v1/");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        model = modelName;
    }

    public string GetModel()
    {
        return model;
    }

    public bool IsConnected()
    {
        HttpResponseMessage response = client.GetAsync("models").GetAwaiter().GetResult();
        return response.IsSuccessStatusCode;
    }

    public void Close()
    {
        client.Dispose();
    }



}
