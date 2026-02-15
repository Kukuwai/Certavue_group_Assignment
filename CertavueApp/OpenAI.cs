using System;
using System.Net.Http;
using System.Net.Http.Headers;

public class OpenAI
{
    private HttpClient client;
    private string model;

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
