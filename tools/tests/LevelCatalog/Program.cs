using BeatEngine.Core.Game;
using System.Text;
using System.Text.Json;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static void Reject(string json)
{
    using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
    try { LevelCatalog.Load(stream); }
    catch (InvalidDataException) { return; }
    throw new Exception("Invalid catalog was accepted: " + json);
}

var path = Path.Combine(AppContext.BaseDirectory, "levels.json");
LevelCatalog catalog;
using (var stream = File.OpenRead(path)) catalog = LevelCatalog.Load(stream);
using (var source = JsonDocument.Parse(File.ReadAllText(path)))
{
    Check(source.RootElement.GetArrayLength() == 1009, "Missing levels");
    foreach (var record in source.RootElement.EnumerateArray())
    {
        int id = record.GetProperty("id").GetInt32();
        var loaded = catalog.GetLevel(id);
        Check(loaded.Id == id && loaded.Theme == record.GetProperty("theme").GetString(), "ID/theme mismatch");
        var words = record.GetProperty("words").EnumerateArray().ToArray();
        Check(loaded.Words.Length == words.Length, "Word count mismatch");
        for (int row = 0; row < words.Length; row++)
            Check(loaded.Words[row].SequenceEqual(words[row].EnumerateArray().Select(s => s.GetString())),
                $"Syllables changed in level {id}, row {row}");
    }
}
Check(catalog.GetLevel(1).Words.SelectMany(w => w).Contains("LÁC"), "Spanish accent lost");
Check(ReferenceEquals(catalog.GetLevel(5), catalog.GetLevel(5)), "Catalog not cached");
try { catalog.GetLevel(1010); throw new Exception("Missing level was accepted"); }
catch (InvalidDataException) { }
Reject("[]");
Reject("null");
Reject("[null]");
Reject("[{\"id\":1,\"theme\":\"ANIMALES\",\"words\":[[\"CA\",\"BA\",\"LLO\"]]}]");
var valid = JsonSerializer.Serialize(catalog.GetLevel(5));
Reject("[" + valid + "," + valid + "]");
Console.WriteLine("PASS: all 1009 levels load with unchanged IDs, themes, syllables and accents.");
Console.WriteLine("PASS: cached lookup and rejection of missing IDs, empty, malformed and duplicate levels.");
