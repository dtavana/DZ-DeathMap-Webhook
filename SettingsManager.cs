using Newtonsoft.Json;
using System.Collections.Generic;
namespace DZ_DeathMap_Webhook
{
    class SettingsManager
    {
        public static Dictionary<string, string> GetSettings()
        {
            string jsonString = System.IO.File.ReadAllText("./config.json");
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonString);
        }
    }
}
