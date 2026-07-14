using System.Text.Json;

namespace Moad.Engine;

public enum DecisionPresentation
{
    Overlay,
    FullScene,
}

public enum DirectedBeatKind
{
    MoveTo,
    Wait,
    FireAtTarget,
    Message,
}

public readonly record struct NarrativeRect(float X, float Y, float Width, float Height);

public sealed record DecisionEffect(
    IReadOnlyDictionary<string, bool> Flags,
    IReadOnlyDictionary<string, int> RelationshipDeltas,
    string? DirectedSequenceId);

public sealed record DecisionChoiceDefinition(
    string Id,
    string Text,
    DecisionEffect Effect);

public sealed record DecisionDefinition(
    string Id,
    string Title,
    string Body,
    DecisionPresentation Presentation,
    float? TimerSeconds,
    int TimeoutChoiceIndex,
    string? TriggerEvent,
    string? TriggerId,
    string? SlideAsset,
    NarrativeRect? SlideCrop,
    IReadOnlyList<DecisionChoiceDefinition> Choices);

public sealed record DirectedBeatDefinition(
    DirectedBeatKind Kind,
    float Duration,
    float? X,
    int? Lane,
    string? TargetId,
    int Count,
    string? Text);

public sealed record DirectedSequenceDefinition(
    string Id,
    IReadOnlyList<DirectedBeatDefinition> Beats);

public sealed class NarrativeContent
{
    public required IReadOnlyDictionary<string, DecisionDefinition> Decisions { get; init; }
    public required IReadOnlyDictionary<string, DirectedSequenceDefinition> DirectedSequences { get; init; }
}

public sealed class NarrativeState
{
    private readonly Dictionary<string, bool> flags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> relationships = new(StringComparer.Ordinal);
    private readonly HashSet<string> resolvedDecisions = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, bool> Flags => flags;
    public IReadOnlyDictionary<string, int> Relationships => relationships;
    public IReadOnlySet<string> ResolvedDecisions => resolvedDecisions;

    internal void Apply(string decisionId, DecisionEffect effect)
    {
        resolvedDecisions.Add(decisionId);
        foreach (var (id, value) in effect.Flags)
        {
            flags[id] = value;
        }
        foreach (var (id, delta) in effect.RelationshipDeltas)
        {
            relationships[id] = relationships.GetValueOrDefault(id) + delta;
        }
    }

}

public static class NarrativeContentLoader
{
    public static NarrativeContent Load(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var decisions = new Dictionary<string, DecisionDefinition>(StringComparer.Ordinal);
        foreach (var item in root.GetProperty("decisions").EnumerateArray())
        {
            var choices = item.GetProperty("choices").EnumerateArray().Select(ParseChoice).ToList();
            if (choices.Count is < 1 or > 4)
            {
                throw new InvalidDataException($"Decision '{item.GetProperty("id").GetString()}' must have one to four choices");
            }
            var id = RequiredString(item, "id");
            var timer = OptionalSingle(item, "timer_seconds");
            var timeoutChoice = OptionalInt(item, "timeout_choice_index") ?? 0;
            if (timeoutChoice < 0 || timeoutChoice >= choices.Count)
            {
                throw new InvalidDataException($"Decision '{id}' has an invalid timeout choice index");
            }
            decisions.Add(id, new DecisionDefinition(
                id,
                RequiredString(item, "title"),
                RequiredString(item, "body"),
                ParseEnum(item, "presentation", DecisionPresentation.Overlay),
                timer,
                timeoutChoice,
                OptionalString(item, "trigger_event"),
                OptionalString(item, "trigger_id"),
                OptionalString(item, "slide_asset"),
                OptionalRect(item, "slide_crop"),
                choices));
        }

        var sequences = new Dictionary<string, DirectedSequenceDefinition>(StringComparer.Ordinal);
        foreach (var item in root.GetProperty("directed_sequences").EnumerateArray())
        {
            var id = RequiredString(item, "id");
            var beats = item.GetProperty("beats").EnumerateArray().Select(ParseBeat).ToList();
            sequences.Add(id, new DirectedSequenceDefinition(id, beats));
        }
        return new NarrativeContent { Decisions = decisions, DirectedSequences = sequences };
    }

    private static DecisionChoiceDefinition ParseChoice(JsonElement item)
    {
        var effect = item.GetProperty("effect");
        var flags = effect.TryGetProperty("flags", out var flagElement)
            ? flagElement.EnumerateObject().ToDictionary(value => value.Name, value => value.Value.GetBoolean(), StringComparer.Ordinal)
            : new Dictionary<string, bool>(StringComparer.Ordinal);
        var relationships = effect.TryGetProperty("relationships", out var relationshipElement)
            ? relationshipElement.EnumerateObject().ToDictionary(value => value.Name, value => value.Value.GetInt32(), StringComparer.Ordinal)
            : new Dictionary<string, int>(StringComparer.Ordinal);
        return new DecisionChoiceDefinition(
            RequiredString(item, "id"),
            RequiredString(item, "text"),
            new DecisionEffect(flags, relationships, OptionalString(effect, "directed_sequence")));
    }

    private static DirectedBeatDefinition ParseBeat(JsonElement item) => new(
        ParseEnum(item, "kind", DirectedBeatKind.Wait),
        OptionalSingle(item, "duration") ?? 0f,
        OptionalSingle(item, "x"),
        OptionalInt(item, "lane"),
        OptionalString(item, "target_id"),
        OptionalInt(item, "count") ?? 1,
        OptionalString(item, "text"));

    private static T ParseEnum<T>(JsonElement item, string name, T fallback) where T : struct, Enum
    {
        var value = OptionalString(item, name);
        if (value is null)
        {
            return fallback;
        }
        var normalized = value.Replace("_", string.Empty, StringComparison.Ordinal);
        foreach (var candidate in Enum.GetValues<T>())
        {
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }
        throw new InvalidDataException($"Unknown {typeof(T).Name} value '{value}' in '{name}'");
    }

    private static string RequiredString(JsonElement item, string name) =>
        item.GetProperty(name).GetString() ?? throw new InvalidDataException($"'{name}' cannot be null");

    private static string? OptionalString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetString() : null;

    private static float? OptionalSingle(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetSingle() : null;

    private static int? OptionalInt(JsonElement item, string name) =>
        item.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null ? value.GetInt32() : null;

    private static NarrativeRect? OptionalRect(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        var components = value.EnumerateArray().Select(component => component.GetSingle()).ToArray();
        if (components.Length != 4 || components[2] <= 0f || components[3] <= 0f)
        {
            throw new InvalidDataException($"'{name}' must be [x, y, width, height] with positive size");
        }
        return new NarrativeRect(components[0], components[1], components[2], components[3]);
    }
}
