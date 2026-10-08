using System.Linq;
using Content.Shared._Moffstation.Objectives;
using Content.Shared._Starlight.Objectives.ObjectivePicker;

// ReSharper disable once CheckNamespace
namespace Content.Client._Moffstation.ObjectivePicker;

public sealed partial class ObjectivePickerUIController
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ObjectivePickerReply>(SLOnReply);
    }

    private void SLToggleSelection(NetEntity id)
    {
        if (_window == null || _window.SLPending || _window.SLOffers is not { } offers)
            return;
        if (!_window.SelectedObjectives.Remove(id) && ObjectivePickerSelection.Available(offers, id) &&
            ObjectivePickerSelection.Compatible(offers, id, _window.SelectedObjectives))
            _window.SelectedObjectives.Add(id);
        _window.UpdateState();
    }

    private void SLSubmit(HashSet<NetEntity> selected, NetEntity mind)
    {
        if (_window == null || _window.SLPending || _window.SLOffers is not { } offers ||
            !ObjectivePickerSelection.Valid(offers, selected))
            return;

        _window.SLSetPending(true);
        _net.SendSystemNetworkMessage(new ObjectivePickerSelected
        {
            MindId = mind,
            SelectedObjectives = new HashSet<NetEntity>(selected),
        });
    }

    private void SLRandomize()
    {
        if (_window == null || _window.SLPending || _window.SLOffers is not { } offers)
            return;
        var order = offers.ObjectiveOptions.Keys.OrderBy(_ => _random.Next()).ToArray();
        if (!ObjectivePickerSelection.TryComplete(offers, order, Array.Empty<NetEntity>(), out var selected))
            return;
        _window.SelectedObjectives.Clear();
        _window.SelectedObjectives.UnionWith(selected);
        _window.UpdateState();
    }

    private void SLMulligan(NetEntity mind)
    {
        if (_window == null || _window.SLPending || _window.SLOffers is not { MulliganUsed: false } offers ||
            _window.SelectedObjectives.Count != 1)
            return;
        var retained = _window.SelectedObjectives.Single();
        if (!ObjectivePickerSelection.Available(offers, retained))
            return;
        _window.SLSetPending(true);
        _net.SendSystemNetworkMessage(new ObjectivePickerMulligan
        {
            MindId = mind,
            RetainedObjective = retained,
        });
    }

    private void SLOnReply(ObjectivePickerReply ev, EntitySessionEventArgs args)
    {
        if (ev.Accepted && ev.Finished)
        {
            _window?.Close();
            _characterInfo.RequestCharacterInfo();
            return;
        }
        _window?.SLSetPending(false, ev.Message);
    }
}
