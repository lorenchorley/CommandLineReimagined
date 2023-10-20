using CommandLine.Modules;
using CommandLineReimagined.Commands;
using CommandLineReimagined.Console;
using CommandLineReimagined.Console.Components;
using CommandLineReimagined.Rendering;
using EntityComponentSystem;
using EntityComponentSystem.RayCasting;
using System;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace CommandLineReimagined;

/// <summary>
/// La fenêtre principale qui héberge le canvas et d'autres composants qui permet l'intéraction
/// </summary>
public partial class MainWindow : Window
{
    private const int ACTIVE_FRAME_RATE = 30;

    private readonly ConsoleLayout _consoleRenderer;
    private readonly RenderLoop _renderLoop;
    private readonly Shell _commandLine;
    private readonly ECS _ecs;
    private readonly CommandHistoryModule _commandHistoryModule;
    private readonly PathModule _pathModule;
    private readonly RayCaster _rayCaster;

    public MainWindow(ConsoleLayout consoleRenderer, RenderLoop renderLoop, Shell commandLine, ECS ecs, CommandHistoryModule commandHistoryModule, PathModule pathModule, RayCaster rayCaster)
    {
        InitializeComponent();

        Canvas.Loaded += Canvas_Loaded;
        Canvas.MouseDown += Canvas_MouseDown;
        Canvas.SizeChanged += Canvas_SizeChanged;

        Input.TextChanged += Input_TextChanged;
        Input.Loaded += Input_Loaded;
        Input.LostFocus += Input_LostFocus;
        Input.KeyUp += Input_KeyUp;
        //Input.PreviewKeyDown += Input_PreviewKeyDown;

        _renderLoop = renderLoop;
        _consoleRenderer = consoleRenderer;
        _commandLine = commandLine;
        _ecs = ecs;
        _commandHistoryModule = commandHistoryModule;
        _pathModule = pathModule;
        _rayCaster = rayCaster;

        renderLoop.SetDrawAction(_consoleRenderer.Draw);
        renderLoop.SetRenderToScreenAction(UpdateVisual);
    }

    #region Mouse input
    private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
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

    private Entity? _contexteMenuEntity;

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

        ContextMenu? navigationContextMenu = FindResource(menu.ContextMenuName) as ContextMenu;

        if (navigationContextMenu != null)
        {
            navigationContextMenu.PlacementTarget = canvas;
            navigationContextMenu.IsOpen = true;

            e.Handled = true;
        }

    }
    #endregion

    #region Text input
    private void Input_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshInput(e.Changes.Count > 0);
    }

    public void RefreshInput(bool textChanged)
    {
        // Extraction du texte pour le rendre dans la console
        _consoleRenderer.Input.ActiveLine.Clear();
        Entity entity = _ecs.NewEntity("Input prompt");

        var textBlock = entity.AddComponent<Console.Components.TextBlock>();
        textBlock.Text = _pathModule.CurrentFolder + "> ";
        _consoleRenderer.Input.ActiveLine.AddLineSegment(textBlock);

        entity = _ecs.NewEntity("Input Textblock");
        textBlock = entity.AddComponent<Console.Components.TextBlock>();
        textBlock.Text = Input.Text;
        _consoleRenderer.Input.ActiveLine.AddLineSegment(textBlock);

        if (textChanged)
            _renderLoop.RefreshOnce();
    }

    //private void Input_PreviewKeyDown(object sender, KeyEventArgs e)
    //{
    //    //if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
    //    //{
    //    //    // Handle Ctrl+Z (Undo) here
    //    //    e.Handled = true; // Prevent the default Undo behavior
    //    //                      // Your custom Undo logic goes here
    //    //}
    //}

    private void Input_KeyUp(object sender, KeyEventArgs e)
    {
        bool needsRefresh =
            _consoleRenderer.Input.SelectionStart != Input.SelectionStart ||
            _consoleRenderer.Input.SelectionLength != Input.SelectionLength;

        // Extraction de la position du curseur pour le rendre dans la console
        _consoleRenderer.Input.SelectionStart = Input.SelectionStart;
        _consoleRenderer.Input.SelectionLength = Input.SelectionLength;

        if (e.Key == Key.Enter)
        {
            _commandLine.ExecuteCommand(Input.Text);
            Input.Text = "";
            needsRefresh = true;
        }

        if (e.Key == Key.Z &&
            (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
            (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            _commandHistoryModule.UndoLastCommand();
            RefreshInput(false);
            needsRefresh = true;
        }

        if (needsRefresh)
            _renderLoop.RefreshOnce();
    }

    private void Input_LostFocus(object sender, RoutedEventArgs e)
    {
        // Maintenir le focus sur l'input
        Input.Focus();
    }

    private void Input_Loaded(object sender, RoutedEventArgs e)
    {
        // Mettre le focus sur l'input initialement
        Input.Focus();
    }
    #endregion

    #region Rendering
    private void UpdateVisual(Bitmap bmp, Action markAsRenderedToScreen)
    {
        //var bmp = buffer.ExtractFinishedFrame();
        var bmpSrc = BmpImageFromBmp(bmp);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            myImage.Source = bmpSrc;
            markAsRenderedToScreen();
        }));
    }

    private static BitmapImage BmpImageFromBmp(Bitmap bmp)
    {
        using (var memory = new System.IO.MemoryStream())
        {
            bmp.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
            memory.Position = 0;

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = memory;
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
    }
    #endregion

    #region Menus contextuels
    private void AddPathToInput_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        string command = $"\"{_contexteMenuEntity.GetComponent<PathInformation>().Path}\"";
        InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    private void Enter_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        // Insérer la commande dans l'input où il y a le curseur
        string command = $"cd \"{_contexteMenuEntity.GetComponent<PathInformation>().Path.GetLowestDirectory()}\"";
        InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    private void CopyPathAsText_PathNavigation_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(_contexteMenuEntity.GetComponent<PathInformation>().Path);
    }

    private void AddPathToInput_FileNavigation_Click(object sender, RoutedEventArgs e)
    {
        string command = $"\"{_contexteMenuEntity.GetComponent<PathInformation>().Path}\"";
        InsertTextAtCursor(command);

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
        InsertTextAtCursor(command);

        _renderLoop.RefreshOnce();
    }

    private void CopyText_Click(object sender, RoutedEventArgs e)
    {
        //Clipboard.SetText("qsd");
    }

    private void InsertTextAtCursor(string command)
    {
        // S'il y a déjà une sélection, on l'ajout à la fin
        if (Input.SelectionLength > 0)
        {
            Input.SelectionStart = Input.SelectionStart + Input.SelectionLength;
            Input.SelectionLength = 0;
        }

        string inputText = Input.Text;
        int cursorPos = Input.CaretIndex;
        Input.Text = inputText.Insert(cursorPos, command);
        Input.CaretIndex = cursorPos;
        Input.SelectionStart = cursorPos;
        Input.SelectionLength = command.Length;
    }

    #endregion

    #region Canvas management
    private void Canvas_Loaded(object sender, RoutedEventArgs e)
    {
        //_renderLoop = new(ACTIVE_FRAME_RATE, (int)Canvas.ActualWidth, (int)Canvas.ActualHeight, _consoleRenderer.Draw, UpdateVisual);
        _renderLoop.SetCanvasSize((int)Canvas.ActualWidth, (int)Canvas.ActualHeight);

        RefreshInput(false);

        _renderLoop.RefreshOnce();
    }

    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        //_buffer.SetSize(e.NewSize.Width, e.NewSize.Height);
    }
    #endregion

    private void MenuItem_Click(object sender, RoutedEventArgs e)
    {

    }
}
