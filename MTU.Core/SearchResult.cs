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

    public override string ToString() => DisplayText;
}
