using Controller;
using EntityComponentSystem;
using EntityComponentSystem.Attributes;
using EntityComponentSystem.RayCasting;
using Interaction.Entities;
using InteractionLogic;
using Rendering;
using System.Drawing;
using System.Reflection;
using UIComponents.Components;

namespace Terminal;

public class MouseInputHandler : InputComponent
{
    private Entity? _contexteMenuEntity;

    [Inject] public RayCaster RayCaster { get; set; }
    [Inject] public LoopController LoopController { get; set; }
    [Inject] public ITextUpdateSystem TextUpdateSystem { get; set; }
    [Inject] public Shell Shell { get; set; }
    [Inject] public ICanvasUpdateSystem CanvasUpdateSystem { get; set; }

    private void OnLeftClick(MouseEventInfo eventInfo)
    {
        var hit = RayCaster.CastRay(eventInfo.MousePosition, InteractableElementLayer.Navigation);

        if (hit == null)
        {
            return;
        }

        // TODO Left click sur des boutons de navigation

        eventInfo.Handled = true;
    }

    public override void OnDoubleLeftClick(MouseEventInfo eventInfo)
    {
        var hit = RayCaster.CastRay(eventInfo.MousePosition, InteractableElementLayer.Navigation);

        if (hit == null)
        {
            return;
        }

        _contexteMenuEntity = hit.Entity;
        DoubleClickAction? action = hit.Entity.TryGetComponent<DoubleClickAction>();

        if (action == null)
        {
            return;
        }

        switch (action.ActionName)
        {
            case "Enter":
                Enter_PathNavigation_Click();
                break;
            case "Up":
                Up_PathNavigation_Click();
                break;
            default:
                throw new NotImplementedException("Double click action type : " + action.ActionName);
        }
    }

    private void OnRightClick(MouseEventInfo eventInfo)
    {
        CastResult hit = RayCaster.CastRay(eventInfo.MousePosition, InteractableElementLayer.Navigation);

        if (hit.Entity == null)
        {
            return; // TODO Le hitbox n'est plus probablement initialisé ou rensiegné00
        }

        _contexteMenuEntity = hit.Entity;
        ContextMenuSource? menu = hit.Entity.TryGetComponent<ContextMenuSource>();

        if (menu == null)
        {
            return;
        }

        CanvasUpdateSystem.OpenContextMenu(menu.ContextMenuName);
        eventInfo.Handled = true;

        //if (!_contextMenuAccessor.TryGet(menu.ContextMenuName, out ContextMenu? navigationContextMenu))
        //{
        //    throw new Exception();
        //}

        //if (navigationContextMenu != null)
        //{
        //    // Ouvrir le menu contextuel
        //    navigationContextMenu.PlacementTarget = canvas;
        //    navigationContextMenu.IsOpen = true;

        //    eventInfo.Handled = true;
        //}
    }

    public void AddPathToInput_PathNavigation_Click()
    {
        string command = $"\"{_contexteMenuEntity!.GetComponent<PathInformation>().Path}\"";
        TextUpdateSystem.InsertTextAtCursor(command);

        LoopController.RequestLoop();
    }

    public void Up_PathNavigation_Click()
    {
        string command = $"up";

        TextUpdateSystem.InsertTextAtCursor(command);
        Shell.ExecuteCurrentPrompt();

        LoopController.RequestLoop();
    }

    public void Enter_PathNavigation_Click()
    {
        // Insérer la commande dans l'input où il y a le curseur
        string command = $"cd \"{_contexteMenuEntity!.GetComponent<PathInformation>().Path.GetLowestDirectory()}\"";

        TextUpdateSystem.InsertTextAtCursor(command);
        Shell.ExecuteCurrentPrompt();

        LoopController.RequestLoop();
    }

    public void CopyPathAsText_PathNavigation_Click()
    {
        TextUpdateSystem.SetClipboardText(_contexteMenuEntity!.GetComponent<PathInformation>().Path);
    }

    public void AddPathToInput_FileNavigation_Click()
    {
        string command = $"\"{_contexteMenuEntity!.GetComponent<PathInformation>().Path}\"";
        TextUpdateSystem.InsertTextAtCursor(command);

        LoopController.RequestLoop();
    }

    public void CopyPathAsText_FileNavigation_Click()
    {
        string path = _contexteMenuEntity!.GetComponent<PathInformation>().Path;
        TextUpdateSystem.SetClipboardText(Path.GetDirectoryName(path));
    }

    public void CopyFileNameAsText_FileNavigation_Click()
    {
        string path = _contexteMenuEntity!.GetComponent<PathInformation>().Path;
        TextUpdateSystem.SetClipboardText(Path.GetFileName(path));
    }

    public void CopyFullPathAsText_FileNavigation_Click()
    {
        string path = _contexteMenuEntity!.GetComponent<PathInformation>().Path;
        TextUpdateSystem.SetClipboardText(path);
    }

    public void Delete_Click()
    {
        // Insérer la commande dans l'input où il y a le curseur
        string command = $"rm {_contexteMenuEntity!.GetComponent<PathInformation>().Path}";
        TextUpdateSystem.InsertTextAtCursor(command);

        LoopController.RequestLoop();
    }

    public void CopyText_Click()
    {
        //Clipboard.SetText("qsd");
    }

}
