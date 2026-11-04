namespace MTU.Core;

/// <summary>Quantity and attachments belong to a character's equipment, not the shared catalogue.</summary>
public sealed class CharacterEquipmentDetails
{
    public EquipmentItem Equipment { get; init; } = new();
    public int Quantity { get; set; } = 1;
    public bool Equipped { get; set; }
    public bool Scope { get; set; }
    public bool TargetingSystem { get; set; }
    public bool LaserSight { get; set; }
    public bool HolographicSight { get; set; }
    public bool Fof { get; set; }
    public bool Phud { get; set; }
    public bool SmartTracker { get; set; }
    public bool XRayOutliningAid { get; set; }
    public bool SupportsAttachments =>
        Equipment.Kind == EquipmentKind.Weapon && Equipment.Range > 0;
    public bool IntelligentWeapon => SupportsAttachments && SmartTracker;

    public CharacterEquipmentDetails Copy() => (CharacterEquipmentDetails)MemberwiseClone();

    public string AttachmentSummary
    {
        get
        {
            if (!SupportsAttachments)
                return "";
            return string.Join(
                ", ",
                new[]
                {
                    (Scope, "Scope"),
                    (TargetingSystem, "Targeting System"),
                    (LaserSight, "Laser Sights"),
                    (HolographicSight, "Holographic Sights"),
                    (Fof, "FOF"),
                    (Phud, "P-HUD"),
                    (SmartTracker, "Smart Tracker (Intelligent Weapon)"),
                    (XRayOutliningAid, "X-Ray Outlining Aid"),
                }
                    .Where(option => option.Item1)
                    .Select(option => option.Item2)
            );
        }
    }
}
