using Application.FrameworkAccessors;
using EntityComponentSystem;
using Rendering;
using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Application.UpdateHandlers;

public class CanvasUpdateHandler : ICanvasUpdateSystem, IECSSystem
{
    private readonly CanvasAccessor _canvasAccessor;
    private readonly ContextMenuAccessor _contextMenuAccessor;

    public CanvasUpdateHandler(CanvasAccessor canvasAccessor, ContextMenuAccessor contextMenuAccessor)
    {
        _canvasAccessor = canvasAccessor;
        _contextMenuAccessor = contextMenuAccessor;
    }

    public void OnInit()
    {
    }

    public void OnStart()
    {
    }

    public void UpdateVisual(Bitmap bmp, Action markAsRenderedToScreen)
    {
        //var bmp = buffer.ExtractFinishedFrame();
        var bmpSrc = BmpImageFromBmp(bmp);
        _canvasAccessor.Dispatch(() =>
        {
            _canvasAccessor.CanvasImage!.Source = bmpSrc;
            markAsRenderedToScreen();
        });
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

    public void OpenContextMenu(string contextMenuName)
    {
        if (!_contextMenuAccessor.TryGet(contextMenuName, out ContextMenu? navigationContextMenu))
        {
            throw new Exception();
        }

        if (navigationContextMenu != null)
        {
            // Ouvrir le menu contextuel
            navigationContextMenu.PlacementTarget = _canvasAccessor.Canvas;
            navigationContextMenu.IsOpen = true;


        }
    }

}
