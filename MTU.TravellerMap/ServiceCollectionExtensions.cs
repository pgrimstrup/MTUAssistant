using Microsoft.Extensions.DependencyInjection;

namespace MTU.TravellerMap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTravellerMapClient(this IServiceCollection services)
    {
        services.AddHttpClient<TravellerMapClient>(c =>
        {
            c.BaseAddress = TravellerMapClient.DefaultBaseAddress;
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MTUAssistant");
        });
        return services;
    }
}
