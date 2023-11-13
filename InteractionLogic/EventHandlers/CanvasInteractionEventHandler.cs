using UIComponents;
using UIComponents.Components;
using EntityComponentSystem;
using EntityComponentSystem.RayCasting;
using InteractionLogic.FrameworkAccessors;
using Rendering;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Terminal;

namespace InteractionLogic.EventHandlers;

public class CanvasInteractionEventHandler
{
    private readonly CanvasAccessor _canvasAccessor;
    private readonly RenderLoop _renderLoop;
    private readonly RayCaster _rayCaster;
    private readonly TextInputUpdateHandler _textInputUpdateHandler;
    private readonly ContextMenuAccessor _contextMenuAccessor;
    private readonly TextInputHandler _textInputHandler;
    private readonly Shell _shell;
    private readonly ECS _ecs;

    private ConsoleLayout ConsoleLayout { get; set; }

    public CanvasInteractionEventHandler(CanvasAccessor canvasAccessor, RenderLoop renderLoop, RayCaster rayCaster, TextInputUpdateHandler textInputUpdateHandler, ContextMenuAccessor contextMenuAccessor, TextInputHandler textInputHandler, Shell shell, ECS ecs)
    {
        _canvasAccessor = canvasAccessor;
        _renderLoop = renderLoop;
        _rayCaster = rayCaster;
        _textInputUpdateHandler = textInputUpdateHandler;
        _contextMenuAccessor = contextMenuAccessor;
        _textInputHandler = textInputHandler;
        _shell = shell;
        _ecs = ecs;
        _canvasAccessor.RegisterEventHandlers<CanvasInteractionEventHandler>(RegisterEventHandlers, UnregisterEventHandlers);

        _contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Enter", Enter_PathNavigation_Click);
        _contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Copy path as text", CopyPathAsText_PathNavigation_Click);
        _contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Add path to input", AddPathToInput_PathNavigation_Click);
        _contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Delete", Delete_Click);

        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Add path to input", AddPathToInput_FileNavigation_Click);
        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Delete", Delete_Click);
        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy filename as text", CopyFileNameAsText_FileNavigation_Click);
        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy path as text", CopyPathAsText_FileNavigation_Click);
        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy full path as text", CopyFullPathAsText_FileNavigation_Click);

        _contextMenuAccessor.RegisterOnClick("TextSelectionContextMenu", "Copy", CopyText_Click);
     
        _shell.OnInit += Shell_OnInit;
    }

    private void Shell_OnInit(object? sender, EventArgs e)
    {
        ConsoleLayout = _ecs.SearchForEntityWithComponent<ConsoleLayout>("Layout") ?? throw new Exception("No console layout found");

    }

    private void RegisterEventHandlers(Canvas canvas, Image image)
    {
        canvas.MouseDown += Canvas_MouseDown;
    }

    private void UnregisterEventHandlers(Canvas canvas, Image image)
    {
        canvas.MouseDown -= Canvas_MouseDown;
    }

    public void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas)
        {
            return;
        }

        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 1)
        {
            HandleLeftClick(e, canvas);
            return;
        }

        if (e.ChangedButton == MouseButton.Left && e.ClickCount == 2)
        {
            HandleDoubleLeftClick(e, canvas);
            return;
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            HandleRightClick(e, canvas);
            return;
        }
    }

    private void HandleDoubleLeftClick(MouseButtonEventArgs e, Canvas canvas)
    {
        Point pos = e.GetPosition(canvas);
        var hit = _rayCaster.CastRay(new System.Drawing.Point((int)pos.X, (int)pos.Y), InteractableElementLayer.Navigation);

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
                Enter_PathNavigation_Click(this, new());
                break;
            case "Up":
                Up_PathNavigation_Click(this, new());
                break;
            default:
                throw new NotImplementedException("Double click action type : " + action.ActionName);
        }
    }

    private void HandleLeftClick(MouseButtonEventArgs e, Canvas canvas)
    {
        Point pos = e.GetPosition(canvas);
        var hit = _rayCaster.CastRay(new System.Drawing.Point((int)pos.X, (int)pos.Y), InteractableElementLayer.Navigation);

        if (hit == null)
        {
            return;
        }

        // TODO Left click sur des boutons de navigation

        e.Handled = true;
    }

    private Entity? _contexteMenuEntity;

    private void HandleRightClick(MouseButtonEventArgs e, Canvas canvas)
    {
        Point pos = e.GetPosition(canvas);
        CastResult hit = _rayCaster.CastRay(new System.Drawing.Point((int)pos.X, (int)pos.Y), InteractableElementLayer.Navigation);

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

        if (!_contextMenuAccessor.TryGet(menu.ContextMenuName, out ContextMenu? navigationContextMenu))
        {
            throw new Exception();
        }

        if (navigationContextMenu != null)
        {
            // Ouvrir le menu contextuel
            navigationContextMenu.PlacementTarget = canvas;
            navigationContextMenu.IsOpen = true;

            e.Handled = true;
        }
    }

    public void AddPathToInput_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        string command = $"\"{_contexteMenuEntity!.GetComponent<PathInformation>().Path}\"";
        _textInputUpdateHandler.InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    public void Up_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        string command = $"up";

        _textInputUpdateHandler.InsertTextAtCursor(command);
        _textInputHandler.ExecuteActiveLine();

        _renderLoop.RefreshOnce();
    }

    public void Enter_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        // Insérer la commande dans l'input où il y a le curseur
        string command = $"cd \"{_contexteMenuEntity!.GetComponent<PathInformation>().Path.GetLowestDirectory()}\"";

        _textInputUpdateHandler.InsertTextAtCursor(command);
        _textInputHandler.ExecuteActiveLine();

        _renderLoop.RefreshOnce();
    }

    public void CopyPathAsText_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_contexteMenuEntity!.GetComponent<PathInformation>().Path);
    }

    public void AddPathToInput_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string command = $"\"{_contexteMenuEntity!.GetComponent<PathInformation>().Path}\"";
        _textInputUpdateHandler.InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    public void CopyPathAsText_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string path = _contexteMenuEntity!.GetComponent<PathInformation>().Path;
        Clipboard.SetText(Path.GetDirectoryName(path));
    }

    public void CopyFileNameAsText_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string path = _contexteMenuEntity!.GetComponent<PathInformation>().Path;
        Clipboard.SetText(Path.GetFileName(path));
    }

    public void CopyFullPathAsText_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string path = _contexteMenuEntity!.GetComponent<PathInformation>().Path;
        Clipboard.SetText(path);
    }

    public void Delete_Click(object sender, RoutedEventArgs e)
    {
        // Insérer la commande dans l'input où il y a le curseur
        string command = $"rm {_contexteMenuEntity!.GetComponent<PathInformation>().Path}";
        _textInputUpdateHandler.InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    public void CopyText_Click(object sender, RoutedEventArgs e)
    {
        //Clipboard.SetText("qsd");
    }

}
