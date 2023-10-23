using System.Drawing;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace InteractionLogic;

public class CanvasUpdateHandler
{
    private readonly CanvasAccessor _canvasAccessor;

    public CanvasUpdateHandler(CanvasAccessor canvasAccessor)
    {
        _canvasAccessor = canvasAccessor;
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
}
