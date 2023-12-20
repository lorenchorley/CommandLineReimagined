using Rendering.Spaces;
using System.Drawing;
using System.Numerics;
using Utils.Math;

namespace Rendering.Tests;

[TestClass]
public class SpacesTests
{
    private const int _height = 1000;
    private const int _width = 1000;

    [TestMethod]
    [TestCategory("Space transformation")]
    public void UIToScreenSpace()
    {
        // Arrange
        // =======
        var screenSpace = new PhysicalScreenSpace();

        screenSpace.SetSize(_width, _height);

        var uiZero = new PointF(0, 0);
        var uiOne = new PointF(1, 1);


        // Act
        // ===
        var uiZeroOnScreen = screenSpace.TransformPointFromUISpace(uiZero);
        var uiOneOnScreen = screenSpace.TransformPointFromUISpace(uiOne);

        var uiTopLeftOnScreen = screenSpace.TransformPointFromUISpace(ConceptualUISpace.TopLeft);
        var uiTopRightOnScreen = screenSpace.TransformPointFromUISpace(ConceptualUISpace.TopRight);
        var uiBottomLeftOnScreen = screenSpace.TransformPointFromUISpace(ConceptualUISpace.BottomLeft);
        var uiBottomRightOnScreen = screenSpace.TransformPointFromUISpace(ConceptualUISpace.BottomRight);

        var middleUITopPoint = MathFEx.Lerp(ConceptualUISpace.TopLeft, ConceptualUISpace.TopRight, 0.5f);
        var middleUITopOnScreen = screenSpace.TransformPointFromUISpace(middleUITopPoint);

        var middleUIBottom = MathFEx.Lerp(ConceptualUISpace.BottomLeft, ConceptualUISpace.BottomRight, 0.5f);
        var middleUIBottomOnScreen = screenSpace.TransformPointFromUISpace(middleUIBottom);

        var middleUILeft = MathFEx.Lerp(ConceptualUISpace.TopLeft, ConceptualUISpace.BottomLeft, 0.5f);
        var middleUILeftOnScreen = screenSpace.TransformPointFromUISpace(middleUILeft);

        var middleUIRight = MathFEx.Lerp(ConceptualUISpace.TopRight, ConceptualUISpace.BottomRight, 0.5f);
        var middleUIRightOnScreen = screenSpace.TransformPointFromUISpace(middleUIRight);

        var middleUiOnScreen = screenSpace.TransformPointFromUISpace(new PointF(0.5f, 0.5f));


        // Assert
        // ======

        // The zero point of the UI space should correspond to the bottom left of the screen space 
        Assert.AreEqual(new PointF(0, _height), uiZeroOnScreen);

        // The one point of the UI space should correspond to the top right of the screen space
        Assert.AreEqual(new PointF(_width, 0), uiOneOnScreen);

        Assert.AreEqual(uiZero, ConceptualUISpace.BottomLeft);
        Assert.AreEqual(uiOne, ConceptualUISpace.TopRight);

        Assert.AreEqual(screenSpace.TopLeft, uiTopLeftOnScreen);
        Assert.AreEqual(screenSpace.TopRight, uiTopRightOnScreen);
        Assert.AreEqual(screenSpace.BottomLeft, uiBottomLeftOnScreen);
        Assert.AreEqual(screenSpace.BottomRight, uiBottomRightOnScreen);

        Assert.AreEqual(new PointF(_width / 2, 0), middleUITopOnScreen);
        Assert.AreEqual(new PointF(_width / 2, _height), middleUIBottomOnScreen);
        Assert.AreEqual(new PointF(0, _height / 2), middleUILeftOnScreen);
        Assert.AreEqual(new PointF(_width, _height / 2), middleUIRightOnScreen);

        Assert.AreEqual(new PointF(_width / 2, _height / 2), middleUiOnScreen);
    }

    [TestMethod]
    [TestCategory("Space transformation")]
    public void ScreenToUISpace()
    {
        // Arrange
        // =======
        var screenSpace = new PhysicalScreenSpace();
        var uiSpace = new ConceptualUISpace(screenSpace);

        screenSpace.SetSize(_width, _height);

        var screenZero = new PointF(0, 0);
        var screenOppositeZero = new PointF(_width, _height);


        // Act
        // ===
        var screenZeroOnUI = uiSpace.TransformFromScreenSpace(screenZero);
        var screenOppositeZeroOnUI = uiSpace.TransformFromScreenSpace(screenOppositeZero);

        var screenTopLeftOnUI = uiSpace.TransformFromScreenSpace(screenSpace.TopLeft);
        var screenTopRightOnUI = uiSpace.TransformFromScreenSpace(screenSpace.TopRight);
        var screenBottomLeftOnUI = uiSpace.TransformFromScreenSpace(screenSpace.BottomLeft);
        var screenBottomRightOnUI = uiSpace.TransformFromScreenSpace(screenSpace.BottomRight);

        var middleScreenTopPoint = MathFEx.Lerp(screenSpace.TopLeft, screenSpace.TopRight, 0.5f);
        var middleScreenTopOnUI = uiSpace.TransformFromScreenSpace(middleScreenTopPoint);

        var middleScreenBottomPoint = MathFEx.Lerp(screenSpace.BottomLeft, screenSpace.BottomRight, 0.5f);
        var middleScreenBottomOnUI = uiSpace.TransformFromScreenSpace(middleScreenBottomPoint);

        var middleScreenLeftPoint = MathFEx.Lerp(screenSpace.TopLeft, screenSpace.BottomLeft, 0.5f);
        var middleScreenLeftOnUI = uiSpace.TransformFromScreenSpace(middleScreenLeftPoint);

        var middleScreenRightPoint = MathFEx.Lerp(screenSpace.TopRight, screenSpace.BottomRight, 0.5f);
        var middleScreenRightOnUI = uiSpace.TransformFromScreenSpace(middleScreenRightPoint);

        var screenMiddleOnUI = uiSpace.TransformFromScreenSpace(new PointF(0.5f * _width, 0.5f * _height));


        // Assert
        // ======

        // The zero point of the UI space should correspond to the bottom left of the screen space 
        Assert.AreEqual(new PointF(0, 1), screenZeroOnUI);

        // The one point of the UI space should correspond to the top right of the screen space
        Assert.AreEqual(new PointF(1, 0), screenOppositeZeroOnUI);

        Assert.AreEqual(screenZero, screenSpace.TopLeft);
        Assert.AreEqual(screenOppositeZero, screenSpace.BottomRight);

        Assert.AreEqual(ConceptualUISpace.TopLeft, screenTopLeftOnUI);
        Assert.AreEqual(ConceptualUISpace.TopRight, screenTopRightOnUI);
        Assert.AreEqual(ConceptualUISpace.BottomLeft, screenBottomLeftOnUI);
        Assert.AreEqual(ConceptualUISpace.BottomRight, screenBottomRightOnUI);

        Assert.AreEqual(new PointF(0.5f, 1), middleScreenTopOnUI);
        Assert.AreEqual(new PointF(0.5f, 0), middleScreenBottomOnUI);
        Assert.AreEqual(new PointF(0, 0.5f), middleScreenLeftOnUI);
        Assert.AreEqual(new PointF(1, 0.5f), middleScreenRightOnUI);

        Assert.AreEqual(new PointF(0.5f, 0.5f), screenMiddleOnUI);
    }
}