using System.Text.Json.Serialization;

namespace EVEye.Models.ZKillboard.Data;

public class ZKillboardEntry
{
    [JsonPropertyName("killmail_id")] 
    public int ID { get; set; }

    [JsonPropertyName("zkb")] 
    public ZKillboardKill ZKillboardKill { get; set; }
}