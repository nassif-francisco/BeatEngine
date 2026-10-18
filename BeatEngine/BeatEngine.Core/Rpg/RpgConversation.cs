using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BeatEngine.Rpg;

public sealed record DialogueLine(string Speaker, string Text);

public sealed class RpgConversation
{
    public string Id { get; set; }
    public string InteractionId { get; set; }
    public string Objective { get; set; }
    public DialogueLine[] Lines { get; set; }
    public DialogueLine[] RepeatLines { get; set; } = Array.Empty<DialogueLine>();
    private bool repeating;
    private DialogueLine[] ActiveLines => repeating ? RepeatLines : Lines;
    public int LineCount => ActiveLines.Length;
    public int LineIndex { get; private set; } = -1;
    public bool Active => LineIndex >= 0;
    public DialogueLine Current => Active ? ActiveLines[LineIndex] : null;

    public static RpgConversation Load(Stream stream)
    {
        var result = JsonSerializer.Deserialize<RpgConversation>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (result == null || string.IsNullOrWhiteSpace(result.Id) || string.IsNullOrWhiteSpace(result.InteractionId) ||
            result.Lines == null || result.Lines.Length == 0 ||
            result.Lines.Concat(result.RepeatLines ?? Array.Empty<DialogueLine>()).Any(line => line == null || string.IsNullOrWhiteSpace(line.Speaker) || string.IsNullOrWhiteSpace(line.Text)))
            throw new InvalidDataException("Invalid conversation script.");
        return result;
    }

    public bool CanStart(RpgWorld world) => !Active && (!world.CompletedScenes.Contains(Id) || RepeatLines?.Length > 0) &&
        world.Map.Interactions.Any(zone => zone.Id == InteractionId && zone.Bounds.Intersects(RpgWorld.BodyAt(world.Position)));

    public bool TryStart(RpgWorld world)
    {
        if (!CanStart(world)) return false;
        world.Stop();
        repeating = world.CompletedScenes.Contains(Id);
        LineIndex = 0;
        return true;
    }

    // Each explicit press advances exactly one line, including consecutive turns by one speaker.
    // True means the final line was acknowledged and the caller must persist the checkpoint now.
    public bool Continue(RpgWorld world)
    {
        if (!Active) return false;
        if (LineIndex + 1 < ActiveLines.Length)
        {
            LineIndex++;
            return false;
        }
        bool completedScene = !repeating;
        if (completedScene) world.CompleteScene(Id);
        LineIndex = -1;
        return completedScene;
    }

    public void Cancel() { LineIndex = -1; repeating = false; }
}
