using MTU.Core;

namespace MTUAssistant.Services;

public sealed class BattlemapState : IDisposable
{
    private readonly CampaignService campaigns;

    public BattlemapState(CampaignService campaigns)
    {
        this.campaigns = campaigns;
        campaigns.Changed += CampaignChanged;
    }

    public SceneSummary? Current { get; private set; }
    public long OpenVersion { get; private set; }
    public event Action? Changed;

    public void Open(SceneSummary scene)
    {
        Current = scene;
        OpenVersion++;
        Changed?.Invoke();
    }

    public void Update(SceneSummary scene)
    {
        if (Current?.Id == scene.Id && Current.CampaignId == scene.CampaignId)
        {
            Current = scene;
            Changed?.Invoke();
        }
    }

    public void Close()
    {
        Current = null;
        OpenVersion++;
        Changed?.Invoke();
    }

    private void CampaignChanged(Campaign? campaign)
    {
        if (Current is not null && Current.CampaignId != campaign?.Id)
            Close();
    }

    public void Dispose() => campaigns.Changed -= CampaignChanged;
}
