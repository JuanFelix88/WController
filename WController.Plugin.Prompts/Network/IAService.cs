using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WController.Plugin.Prompts.Network;
internal static class IAService
{
    public static async Task<IAResponse> RequestPrompt(string prompt)
    {
        HttpClient client = new HttpClient();
        
        StringContent content = new StringContent(JsonConvert.SerializeObject(new { prompt }), Encoding.UTF8, "application/json");

        var iaServiceResponse = await client.PostAsync("http://158.220.101.132:5252/prompt", content);

        if (!iaServiceResponse.IsSuccessStatusCode)
        {
            throw new Exception("IA Service request failed with status code: " + iaServiceResponse.StatusCode);
        }

        var responseString = await iaServiceResponse.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<IAResponse>(responseString);
    }

    #region Subtypes
    internal class IAResponse
    {
        public string Response { get; set; }
        public int PoolNode { get; set; }
        public int PoolNodeUsedCount { get; set; }
        public string AgentName { get; set; }
    }
    #endregion
}
