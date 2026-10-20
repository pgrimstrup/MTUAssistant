using Microsoft.Extensions.DependencyInjection;
using MTU.Core;

namespace MTU.TravellerMap;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTravellerMapClient(this IServiceCollection services)
    {
        services.AddHttpClient<ITravellerMapClient, TravellerMapClient>(c =>
        {
            c.BaseAddress = TravellerMapClient.DefaultBaseAddress;
            c.DefaultRequestHeaders.UserAgent.ParseAdd("MTUAssistant");
        });
        return services;
    }
}
