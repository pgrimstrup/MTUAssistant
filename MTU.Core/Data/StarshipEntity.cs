namespace MTU.Core.Data;

/// <summary>A starship available to the Travellers of one campaign. At most one per campaign is
/// Equipped: the ship the party is using now.</summary>
public class StarshipEntity
{
    public int Id { get; set; }
    public int CampaignId { get; set; }
    public CampaignEntity Campaign { get; set; } = null!;
    public string Name { get; set; } = "";

    /// <summary>The ship's class or role, such as "Free Trader"; shown as Type.</summary>
    public string Classification { get; set; } = "";
    public int TechLevel { get; set; } = 12;

    /// <summary>Hull displacement in tons.</summary>
    public int Displacement { get; set; } = 100;
    public int DamageTaken { get; set; }

    /// <summary>A one-off design: it pays full price for its systems, not 90%.</summary>
    public bool Prototype { get; set; }

    /// <summary>Purchase cost in MCr, worked out from the systems when the ship is saved.</summary>
    public decimal PurchaseCost { get; set; }

    /// <summary>Maintenance cost in credits per month, worked out when the ship is saved.</summary>
    public decimal MaintenanceCost { get; set; }

    /// <summary>Crew positions as typed, such as "Pilot, Engineer x3".</summary>
    public string Crew { get; set; } = "";
    public bool Equipped { get; set; }
}

/// <summary>One system of a starship: its hull, a drive, a stateroom block, cargo space and so on.
/// Systems are listed by Type, then in Sequence order.</summary>
public class StarshipSystemEntity
{
    public int Id { get; set; }
    public int StarshipId { get; set; }
    public StarshipEntity Starship { get; set; } = null!;
    public StarshipSystemType Type { get; set; }
    public int Sequence { get; set; }
    public string Description { get; set; } = "";

    /// <summary>Tons the system takes up.</summary>
    public decimal Displacement { get; set; }

    /// <summary>Cost in MCr.</summary>
    public decimal Cost { get; set; }
    public decimal PowerRequirement { get; set; }
    public int Rating { get; set; }
}
