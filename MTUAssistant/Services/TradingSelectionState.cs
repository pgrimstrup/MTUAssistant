namespace MTUAssistant.Services;

/// <summary>Independent trade selections per campaign within the current user circuit.</summary>
public sealed class TradingSelectionState
{
    private readonly Dictionary<int, TradingSelections> campaigns = [];

    public TradingSelections ForCampaign(int campaignId)
    {
        if (!campaigns.TryGetValue(campaignId, out var selections))
            campaigns[campaignId] = selections = new TradingSelections();
        return selections;
    }
}

public sealed class TradingSelections
{
    public int BuyingTravellerId { get; set; }
    public string BuyingMode { get; set; } = "Broker";
    public int SellingTravellerId { get; set; }
    public string SellingMode { get; set; } = "Broker";
}
