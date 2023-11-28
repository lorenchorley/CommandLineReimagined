using System.Drawing;

namespace Rendering;

public interface ICanvasUpdateSystem
{
    void UpdateVisual(Bitmap bmp, Action markAsRenderedToScreen);
    void OpenContextMenu(string contextMenuName);
}
