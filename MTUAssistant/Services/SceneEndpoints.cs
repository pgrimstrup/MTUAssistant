using MTU.Core;

namespace MTUAssistant.Services;

public static class SceneEndpoints
{
    public static void MapSceneImages(this WebApplication app)
    {
        app.MapGet(
            "/api/scenes/{campaignId:int}/{sceneId:int}/image",
            async (int campaignId, int sceneId, SceneService scenes) =>
            {
                var image = await scenes.ImageAsync(campaignId, sceneId);
                return image is null
                    ? Results.NotFound()
                    : Results.File(image.Bytes, image.ContentType);
            }
        );
        app.MapGet(
            "/api/characters/{campaignId:int}/{characterId:int}/token",
            async (int campaignId, int characterId, SceneService scenes) =>
            {
                var image = await scenes.TokenImageAsync(campaignId, characterId);
                return image is null
                    ? Results.NotFound()
                    : Results.File(image.Bytes, image.ContentType);
            }
        );
    }
}
