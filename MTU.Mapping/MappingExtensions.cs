using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using MTU.Core;

namespace MTU.Mapping;

public sealed class MappingOptions
{
    public string TileRoot { get; set; } = TileRefreshWorker.DefaultRootFolder;
}

public static class MappingExtensions
{
    public static IServiceCollection AddMtuMapping(
        this IServiceCollection services,
        string? tileRoot = null
    )
    {
        services.AddSingleton(
            new MappingOptions { TileRoot = tileRoot ?? TileRefreshWorker.DefaultRootFolder }
        );
        services.AddSingleton<ITileManifestService, TileManifestService>();
        return services;
    }

    /// <summary>Serves the downloaded tile images from /tiles/{scale}/{file}.png.</summary>
    public static IApplicationBuilder UseMtuTiles(this IApplicationBuilder app)
    {
        var root = app.ApplicationServices.GetRequiredService<MappingOptions>().TileRoot;
        Directory.CreateDirectory(root);

        return app.UseStaticFiles(
            new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(root),
                RequestPath = "/tiles",
                OnPrepareResponse = ctx =>
                    ctx.Context.Response.Headers.CacheControl = "public,max-age=3600",
            }
        );
    }

    public static IEndpointRouteBuilder MapMtuTiles(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/map/manifest",
            (ITileManifestService service) => Results.Ok(service.GetManifest())
        );
        return endpoints;
    }

    public static IEndpointRouteBuilder MapMtuMap(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/map/overview",
            async (MapDataService service, CancellationToken ct) =>
                Results.Ok(await service.GetOverviewAsync(ct))
        );
        endpoints.MapGet(
            "/api/map/sectors/{sectorId:int}",
            async (int sectorId, MapDataService service, CancellationToken ct) =>
                await service.GetSectorAsync(sectorId, ct) is { } worlds
                    ? Results.Ok(worlds)
                    : Results.NotFound()
        );
        return endpoints;
    }
}
