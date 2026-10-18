using System.IO;
using System.Linq;
using System.Text.Json;

namespace BeatEngine.Rpg;

public sealed class RpgSceneConfig
{
    public string Name { get; set; } = "La biblioteca";
    public int FrameSize { get; set; } = 32;
    public float ViewHeight { get; set; } = 320;
    public float[] Spawn { get; set; }
    public float[] Bounds { get; set; }

    public static RpgSceneConfig Load(Stream stream)
    {
        var config = JsonSerializer.Deserialize<RpgSceneConfig>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (config == null || config.FrameSize != RpgWorld.FrameSize || config.Spawn?.Length != 2 ||
            config.Bounds?.Length != 4 || config.ViewHeight < 160 || config.ViewHeight > 1080 ||
            !float.IsFinite(config.ViewHeight) || config.Spawn.Concat(config.Bounds).Any(value => !float.IsFinite(value)) ||
            config.Bounds[2] <= 0 || config.Bounds[3] <= 0)
            throw new InvalidDataException("Invalid 32px library scene configuration.");
        return config;
    }
}
