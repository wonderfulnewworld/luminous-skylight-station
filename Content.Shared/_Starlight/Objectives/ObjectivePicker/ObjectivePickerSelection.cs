using System.Linq;

namespace Content.Shared._Starlight.Objectives.ObjectivePicker;

/// <summary>
/// Shared validation and completion search for manual, random, and timeout selections.
/// </summary>
public static class ObjectivePickerSelection
{
    public const float Tolerance = 0.0001f;

    public static float Difficulty(PotentialObjectivesComponent offers, IEnumerable<NetEntity> selected)
        => selected.Distinct().Sum(id => offers.Difficulties.GetValueOrDefault(id));

    public static bool Available(PotentialObjectivesComponent offers, NetEntity id)
        => offers.ObjectiveOptions.ContainsKey(id) && !offers.UnavailableObjectives.Contains(id);

    public static bool Compatible(PotentialObjectivesComponent offers, NetEntity id, IEnumerable<NetEntity> selected)
        => !offers.Conflicts.TryGetValue(id, out var conflicts) || !selected.Any(conflicts.Contains);

    public static bool Valid(PotentialObjectivesComponent offers, IReadOnlyCollection<NetEntity> selected)
    {
        return (selected.Count > 0 || offers.MinimumDifficulty <= Tolerance) && selected.All(id => Available(offers, id) && Compatible(offers, id, selected)) &&
               Difficulty(offers, selected) + Tolerance >= offers.MinimumDifficulty;
    }

    /// <summary>Finds a compatible completion, without imposing a maximum count or difficulty.</summary>
    public static bool TryComplete(PotentialObjectivesComponent offers, IEnumerable<NetEntity> order,
        IEnumerable<NetEntity> retained, out HashSet<NetEntity> selected)
    {
        var result = retained.ToHashSet();
        selected = result;
        if (result.Any(id => !Available(offers, id) || !Compatible(offers, id, result)))
            return false;

        var candidates = order.Distinct().Where(id => Available(offers, id) && !result.Contains(id) &&
            offers.Difficulties.GetValueOrDefault(id) > 0).ToArray();
        return Search(0, Difficulty(offers, result));

        bool Search(int start, float difficulty)
        {
            if (difficulty + Tolerance >= offers.MinimumDifficulty)
                return result.Count > 0 || offers.MinimumDifficulty <= Tolerance;

            var possible = difficulty;
            for (var i = start; i < candidates.Length; i++)
            {
                if (Compatible(offers, candidates[i], result))
                    possible += offers.Difficulties[candidates[i]];
            }
            if (possible + Tolerance < offers.MinimumDifficulty)
                return false;

            for (var i = start; i < candidates.Length; i++)
            {
                var id = candidates[i];
                if (!Compatible(offers, id, result))
                    continue;
                result.Add(id);
                if (Search(i + 1, difficulty + offers.Difficulties[id]))
                    return true;
                result.Remove(id);
            }
            return false;
        }
    }
}
