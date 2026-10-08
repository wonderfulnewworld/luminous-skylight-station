namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

public static class TraitorForcedObjectiveWeights
{
    public static Dictionary<string, float> Create(bool allowGloriousDeath) => new()
    {
        ["EscapeShuttleObjective"] = allowGloriousDeath ? 0.735f : 0.75f,
        ["TraitorAchieveObjectivesObjective"] = allowGloriousDeath ? 0.245f : 0.25f,
        ["DieObjective"] = allowGloriousDeath ? 0.02f : 0f,
    };
}
