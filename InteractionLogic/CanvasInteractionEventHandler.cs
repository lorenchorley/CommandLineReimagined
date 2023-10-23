using CommandLine.Modules;
using Console;
using Console.Components;
using EntityComponentSystem;
using EntityComponentSystem.RayCasting;
using Rendering;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace InteractionLogic;

public class CanvasInteractionEventHandler
{
    private readonly CanvasAccessor _canvasAccessor;
    private readonly RenderLoop _renderLoop;
    private readonly RayCaster _rayCaster;
    private readonly TextInputUpdateHandler _textInputUpdateHandler;

    public CanvasInteractionEventHandler(CanvasAccessor canvasAccessor, RenderLoop renderLoop, RayCaster rayCaster, TextInputUpdateHandler textInputUpdateHandler)
    {
        _canvasAccessor = canvasAccessor;
        _renderLoop = renderLoop;
        _rayCaster = rayCaster;
        _textInputUpdateHandler = textInputUpdateHandler;
    }

    public void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas)
        {
            return;
        }

        if (e.ChangedButton == MouseButton.Right)
        {
            HandleRightClick(e, canvas);
            return;
        }

        if (e.ChangedButton == MouseButton.Left)
        {
            HandleLeftClick(e, canvas);
            return;
        }
    }

    private void HandleLeftClick(MouseButtonEventArgs e, Canvas canvas)
    {
        System.Windows.Point pos = e.GetPosition(canvas);
        var hit = _rayCaster.CastRay(new System.Drawing.Point((int)pos.X, (int)pos.Y), InteractableElementLayer.Navigation);

        if (hit == null)
        {
            return;
        }

        // TODO Left click sur des boutons de navigation

        e.Handled = true;
    }

    public Entity? _contexteMenuEntity { get; private set; }

    private void HandleRightClick(MouseButtonEventArgs e, Canvas canvas)
    {
        System.Windows.Point pos = e.GetPosition(canvas);
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

        ContextMenu? navigationContextMenu = _canvasAccessor.Canvas.FindResource(menu.ContextMenuName) as ContextMenu;

        if (navigationContextMenu != null)
        {
            navigationContextMenu.PlacementTarget = canvas;
            navigationContextMenu.IsOpen = true;

            e.Handled = true;
        }

    }

    private void AddPathToInput_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        string command = $"\"{_contexteMenuEntity.GetComponent<PathInformation>().Path}\"";
        _textInputUpdateHandler.InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    private void Enter_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        // Insérer la commande dans l'input où il y a le curseur
        string command = $"cd \"{_contexteMenuEntity.GetComponent<PathInformation>().Path.GetLowestDirectory()}\"";
        _textInputUpdateHandler.InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    private void CopyPathAsText_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_contexteMenuEntity.GetComponent<PathInformation>().Path);
    }

    private void AddPathToInput_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string command = $"\"{_contexteMenuEntity.GetComponent<PathInformation>().Path}\"";
        _textInputUpdateHandler.InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    private void CopyPathAsText_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string path = _contexteMenuEntity.GetComponent<PathInformation>().Path;
        Clipboard.SetText(Path.GetDirectoryName(path));
    }

    private void CopyFileNameAsText_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string path = _contexteMenuEntity.GetComponent<PathInformation>().Path;
        Clipboard.SetText(Path.GetFileName(path));
    }

    private void CopyFullPathAsText_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string path = _contexteMenuEntity.GetComponent<PathInformation>().Path;
        Clipboard.SetText(path);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        // Insérer la commande dans l'input où il y a le curseur
        string command = $"rm {_contexteMenuEntity.GetComponent<PathInformation>().Path}";
        _textInputUpdateHandler.InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        //Clipboard.SetText("qsd");
    }

}
