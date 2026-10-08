using System.Linq;
using Content.Client.CharacterInfo;
using Content.Client.Gameplay;
using Content.Shared._Moffstation.Objectives;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Random;

namespace Content.Client._Moffstation.ObjectivePicker;

[UsedImplicitly]
public sealed partial class ObjectivePickerUIController : UIController, IOnStateExited<GameplayState>
{
    [Dependency] private IEntityNetworkManager _net = default!;
    [Dependency] private IRobustRandom _random = default!;

    [UISystemDependency] private readonly CharacterInfoSystem _characterInfo = default!;    // Moffstation - Character Menu Redesign

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
        _window.OnMulligan += SLMulligan; // Starlight
    }

    private void OnSelectedChange(NetEntity netEntity) => SLToggleSelection(netEntity); // Starlight

    private void OnSubmitted(HashSet<NetEntity> selectedObjectives, NetEntity mindId) => SLSubmit(selectedObjectives, mindId); // Starlight

    private void OnRandomize() => SLRandomize(); // Starlight: no fixed pick count. // Starlight

    private void OnClear()
    {
        if (_window == null)
            return;

        _window.SelectedObjectives.Clear();
        _window.UpdateState();
    }
}
