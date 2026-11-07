namespace MTU.Core.Data;

public sealed class SceneEntity
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public CampaignEntity Campaign { get; set; } = null!;
    public string Name { get; set; } = "";
    public byte[] Image { get; set; } = [];
    public string ContentType { get; set; } = "image/png";
    public int GridSize { get; set; } = 100;
    public string GridColour { get; set; } = "#000000";
    public int GridOpacity { get; set; } = 50;
    public double GridlineWidth { get; set; } = 2;
    public string? FogState { get; set; }
    public bool FogEnabled { get; set; }
    public string? EncounterState { get; set; }
    public long Revision { get; set; } = 1;
}
