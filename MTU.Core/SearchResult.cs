using System;
using System.Collections.Generic;
using System.Text;

namespace MTU.Core;

public class SearchResult
{
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public int SubsectorId { get; set; }
    public string SubsectorLetter { get; set; } = string.Empty;
    public string SubsectorName { get; set; } = string.Empty;
    public int SystemId { get; set; }
    public string SystemName { get; set; } = string.Empty;
    public string SectorHex { get; set; } = string.Empty;
    public string SubsectorHex { get; set; } = string.Empty;

    public string DisplayText
    {
        get
        {
            if (SystemId > 0)
                return $"{SystemName} ({SectorName} [{SubsectorLetter}], {SubsectorName} {SubsectorHex})";
            if (SubsectorId > 0)
                return $"{SubsectorName} ({SectorName} [{SubsectorLetter}])";
            return $"{SectorName}";
        }
    }

    /// <summary>First line of a search list item: the system, subsector or sector name.</summary>
    public string Title =>
        SystemId > 0 ? SystemName
        : SubsectorId > 0 ? SubsectorName
        : SectorName;

    /// <summary>Second line, left: the subsector, or the kind of result for sectors.</summary>
    public string Subtitle =>
        SystemId > 0 && !string.IsNullOrWhiteSpace(SubsectorName) ? SubsectorName
        : SystemId > 0 || SubsectorId > 0 ? $"Subsector {SubsectorLetter}"
        : "Sector";

    /// <summary>Second line, right: the sector, with the sector hex for systems.</summary>
    public string Location =>
        SystemId > 0 ? $"{SectorName} {SectorHex}"
        : SubsectorId > 0 ? SectorName
        : string.Empty;

    public override string ToString() => DisplayText;
}
