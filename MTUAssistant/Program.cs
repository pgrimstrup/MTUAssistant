using MTU.Core;
using MTU.Mapping;
using MTU.TravellerMap;
using MTUAssistant.Components;
using MTUAssistant.Services;

namespace MTUAssistant
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();

            builder.Services.AddTravellerMapClient();
            builder.Services.AddMtuDatabase();
            builder.Services.AddMtuMapping();
            builder.Services.AddTransient<TileRefreshWorker>();
            builder.Services.AddScoped<TileRefreshService>();
            builder.Services.AddScoped<MapPointerLocation>();
            builder.Services.AddScoped<SelectedWorldState>();

            var app = builder.Build();

            app.Services.MigrateMtuDatabase();
            await app.Services.GetRequiredService<SystemDataProvider>().InitializeAsync();
            // Prepare map projections before requests are accepted.
            _ = app.Services.GetRequiredService<MapDataService>();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseHttpsRedirection();


            app.UseAntiforgery();

            app.MapStaticAssets();
            app.MapMtuMap();
            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            await app.RunAsync();
        }
    }
}
