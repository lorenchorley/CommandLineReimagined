using Application.FrameworkAccessors;
using Application.UpdateHandlers;
using EntityComponentSystem;
using Interaction.Entities;
using InteractionLogic;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Application.EventHandlers;

public class CanvasInteractionEventHandler : IECSSubsystem
{
    private readonly CanvasAccessor _canvasAccessor;
    private readonly TextInputUpdateHandler _textInputUpdateHandler;
    private readonly ContextMenuAccessor _contextMenuAccessor;
    private readonly TextInputHandler _textInputHandler;
    private readonly InputSystem _inputSystem;

    public CanvasInteractionEventHandler(CanvasAccessor canvasAccessor, TextInputUpdateHandler textInputUpdateHandler, ContextMenuAccessor contextMenuAccessor, TextInputHandler textInputHandler, InputSystem inputSystem)
    {
        _canvasAccessor = canvasAccessor;
        _textInputUpdateHandler = textInputUpdateHandler;
        _contextMenuAccessor = contextMenuAccessor;
        _textInputHandler = textInputHandler;
        _inputSystem = inputSystem;
        _canvasAccessor.RegisterEventHandlers<CanvasInteractionEventHandler>(RegisterEventHandlers, UnregisterEventHandlers);

        //_contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Enter", Enter_PathNavigation_Click);
        //_contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Copy path as text", CopyPathAsText_PathNavigation_Click);
        //_contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Add path to input", AddPathToInput_PathNavigation_Click);
        //_contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Delete", Delete_Click);

        //_contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Add path to input", AddPathToInput_FileNavigation_Click);
        //_contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Delete", Delete_Click);
        //_contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy filename as text", CopyFileNameAsText_FileNavigation_Click);
        //_contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy path as text", CopyPathAsText_FileNavigation_Click);
        //_contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy full path as text", CopyFullPathAsText_FileNavigation_Click);

        //_contextMenuAccessor.RegisterOnClick("TextSelectionContextMenu", "Copy", CopyText_Click);
        
    }

    public void OnInit()
    {
    }

    public void OnStart()
    {

        _contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Enter", ContextMenuClickHandler);
        _contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Copy path as text", ContextMenuClickHandler);
        _contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Add path to input", ContextMenuClickHandler);
        _contextMenuAccessor.RegisterOnClick("PathNavigationContextMenu", "Delete", ContextMenuClickHandler);

        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Add path to input", ContextMenuClickHandler);
        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Delete", ContextMenuClickHandler);
        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy filename as text", ContextMenuClickHandler);
        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy path as text", ContextMenuClickHandler);
        _contextMenuAccessor.RegisterOnClick("FileNavigationContextMenu", "Copy full path as text", ContextMenuClickHandler);

        _contextMenuAccessor.RegisterOnClick("TextSelectionContextMenu", "Copy", ContextMenuClickHandler);

    }
    private void RegisterEventHandlers(Canvas canvas, Image image)
    {
        canvas.MouseDown += Canvas_MouseDown;
    }

    private void UnregisterEventHandlers(Canvas canvas, Image image)
    {
        canvas.MouseDown -= Canvas_MouseDown;
    }

    public void ContextMenuClickHandler(object sender, RoutedEventArgs e)
    {
        throw new NotImplementedException();
    }

    public void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Canvas canvas)
        {
            return;
        }

        MouseEventInfo info = new MouseEventInfo
        {
            ButtonState = (Interaction.Entities.MouseButtonState)(int)e.ButtonState,
            ChangedButton = (Interaction.Entities.MouseButton)(int)e.ChangedButton,
            ClickCount = e.ClickCount,
            MousePosition = GetPosition(e, canvas),
        };

        if (info.ChangedButton == Interaction.Entities.MouseButton.Left && info.ClickCount == 1)
        {
            _inputSystem.OnLeftClick(info);
            return;
        }

        if (info.ChangedButton == Interaction.Entities.MouseButton.Left && info.ClickCount == 2)
        {
            _inputSystem.OnDoubleLeftClick(info);
            return;
        }

        if (info.ChangedButton == Interaction.Entities.MouseButton.Right)
        {
            _inputSystem.OnRightClick(info);
            return;
        }
    }

    private static System.Drawing.Point GetPosition(MouseButtonEventArgs e, Canvas canvas)
    {
        var p = e.GetPosition(canvas);
        return new((int)p.X, (int)p.Y);
    }

}
