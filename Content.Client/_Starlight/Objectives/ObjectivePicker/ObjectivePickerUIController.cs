using System.Linq;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using Content.Shared._Starlight.Objectives.ObjectivePicker;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface;
using Robust.Shared.Random;

namespace Content.Client._Starlight.Objectives.ObjectivePicker;

[UsedImplicitly]
public sealed partial class ObjectivePickerUIController : UIController, IOnStateExited<GameplayState>
{
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;

    private ObjectivePickerWindow? _window;

    public void OnStateExited(GameplayState state)
    {
        if (_window == null)
            return;

        _window.Close();
        _window = null;
    }

    public void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<ObjectivePickerWindow>();
        _window.OpenCentered();
        _window.OnClose += () => _window = null;
        _window.OnSelectedChange += OnSelectedChange;
        _window.OnSubmitted += OnSubmitted;
        _window.OnRandomize += OnRandomize;
        _window.OnClear += OnClear;
        _window.OnMulligan += SLMulligan;
    }

    private void OnSelectedChange(NetEntity netEntity) => SLToggleSelection(netEntity);

    private void OnSubmitted(HashSet<NetEntity> selectedObjectives, NetEntity mindId) => SLSubmit(selectedObjectives, mindId);

    private void OnRandomize() => SLRandomize();

    private void OnClear()
    {
        if (_window == null)
            return;

        _window.SelectedObjectives.Clear();
        _window.UpdateState();
    }

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
