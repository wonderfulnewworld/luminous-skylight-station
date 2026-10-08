// ReSharper disable once CheckNamespace
namespace Content.Client._Moffstation.CharacterMenu;

public sealed partial class MoffCharacterWindow
{
    private void SLInitializeCharacterInfo()
    {
        CharacterInfoTabs.SetTabTitle(0, Loc.GetString("character-info-objectives"));
        CharacterInfoTabs.SetTabTitle(1, Loc.GetString("character-info-ic-ooc"));

        // The overview already displays the character's sprite and identity.
        InfoIC.CharacterView.Visible = false;
        InfoOOC.CharacterView.Visible = false;
    }
}
