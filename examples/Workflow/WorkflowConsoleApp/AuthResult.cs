using Newtonsoft.Json;

public class AuthResult
{
    [JsonProperty("approved")]
    public bool approved { get; set; }

    [JsonProperty("summary")]
    public string summary { get; set; }
}
