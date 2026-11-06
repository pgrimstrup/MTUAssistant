namespace MTU.Core.Data;

public sealed class SceneTokenEntity
{
    public int Id { get; set; }
    public int SceneId { get; set; }
    public SceneEntity Scene { get; set; } = null!;
    public int CharacterId { get; set; }
    public CharacterEntity Character { get; set; } = null!;
    public string Name { get; set; } = "";
    public bool HasCustomName { get; set; }
    public int? InstanceNumber { get; set; }
    public bool IsVisible { get; set; } = true;
    public int? Column { get; set; }
    public int? Row { get; set; }
}
