using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Application.FrameworkAccessors;

public class ContextMenuAccessor : KeyedFrameworkElementAccessor<ContextMenu>
{
    public void RegisterOnClick(string contextMenuName, string menuItemHeader, RoutedEventHandler eventHandler)
    {
        MenuItem menuItem = GetMenuItem(contextMenuName, menuItemHeader);

        menuItem.Click += eventHandler;
    }

    public void UnregisterOnClick(string contextMenuName, string menuItemHeader, RoutedEventHandler eventHandler)
    {
        MenuItem menuItem = GetMenuItem(contextMenuName, menuItemHeader);

        menuItem.Click -= eventHandler;
    }

    private MenuItem GetMenuItem(string contextMenuName, string menuItemHeader)
    {
        if (!TryGet(contextMenuName, out ContextMenu? contextMenu))
        {
            throw new Exception();
        }

        MenuItem? menuItem
            = contextMenu.Items
                         .OfType<MenuItem>()
                         .First(item => string.Equals(item.Header.ToString(), menuItemHeader, StringComparison.OrdinalIgnoreCase));

        if (menuItem == null)
        {
            throw new Exception();
        }

        return menuItem;
    }
}

