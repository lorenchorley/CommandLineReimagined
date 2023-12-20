using EntityComponentSystem;
using InteractionLogic;
using Rendering;
using Rendering.Components;
using Rendering.Spaces;
using System;
using System.Drawing;
using System.Numerics;
using static EntityComponentSystem.ECS;

namespace Controller;
public class LoopController
{
    private readonly object _requestLock = new object();
    private Task? EnqueuedRefreshTask = null;

    private readonly ECS _ecs;
    private readonly RenderLoop _renderLoop;
    private readonly ICanvasEventEmitter _canvasEventEmitter;

    private readonly ConceptualUISpace _uiSpace;
    private readonly PhysicalScreenSpace _screenSpace;

    public UICamera[] ShadowCameras { get; private set; } = new UICamera[0];
    public UICamera[] ActiveCameras { get; private set; } = new UICamera[0];

    public LoopController(ECS ecs, RenderLoop renderLoop, ICanvasEventEmitter canvasEventEmitter, PhysicalScreenSpace screenSpace, ConceptualUISpace uiSpace)
    {
        _ecs = ecs;
        _renderLoop = renderLoop;
        _canvasEventEmitter = canvasEventEmitter;
        _screenSpace = screenSpace;
        _uiSpace = uiSpace;

        _canvasEventEmitter.RegisterSizeUpdateHandler(SetCanvasSize);
    }

    public void Start()
    {
        RequestLoop();
    }

    public void RequestLoop()
    {
        lock (_requestLock)
        {
            if (EnqueuedRefreshTask is not null)
                return;

            // Enqueue si pas déjà demandé
            EnqueuedRefreshTask = Task.Delay(15).ContinueWith(_ => MainLoop());
        }
    }

    // Need to shift the responsibility of the main loop to here
    // instead of letting input action dictate the thread and the timing of actions.
    private void MainLoop()
    {
        lock (_requestLock) // Not sure if necessary, also means that other threads can be locked while render loop is running
        {
            // Not ideal that this function is here, but it needs to be done sometime after all operations were completed
            // Might be able to set up a more generalised loop that takes into account the render cycle, the object life cycle, etc
            _ecs.Update();

            UpdateLayoutsInActiveTree(); // Should produce a number of UITransformDifferentials

            ShadowECS shadowECS = _ecs.TriggerMerge();

            UpdateCameraLists(shadowECS);

            _renderLoop.Update(shadowECS);

            EnqueuedRefreshTask = null;
        }
    }

    private void UpdateLayoutsInActiveTree()
    {
        List<UILayoutComponent> layouts = _ecs.ComponentTreeSearchLowestFirst<UILayoutComponent>();

        // Première passe pour savoir quels élements devraient y être et où il faut les placer
        foreach (var layout in layouts)
        {
            layout.RecalculateChildTransforms();
        }
    }

    public void UpdateCameraLists(ShadowECS shadowECS)
    {
        int s = ShadowCameras.Length;
        int a = ActiveCameras.Length;

        ShadowCameras = shadowECS.Components.OfType<UICamera>().ToArray();
        ActiveCameras = _ecs.FlatComponentList.OfType<UICamera>().ToArray(); // Pb de concurrence 

        // Update the camera components if the number of cameras changed
        // A bit too much, but it's a simple way to make sure that all of the camera components are up to date
        if (s != ShadowCameras.Length || a != ActiveCameras.Length)
        {
            UpdateCameraComponents(_cameraWidth, _cameraHeight, _letterSize);
        }
    }

    private int _cameraWidth = 0;
    private int _cameraHeight = 0;
    private SizeF _letterSize;

    public void SetCanvasSize(int width, int height)
    {
        _cameraWidth = width;
        _cameraHeight = height;

        _screenSpace.SetSize(_cameraWidth, _cameraHeight);
        _renderLoop.SetCanvasSize(_cameraWidth, _cameraHeight);

        _letterSize = _renderLoop.GetLetterSize();

        UpdateCameraComponents(_cameraWidth, _cameraHeight, _letterSize);
    }

    private void UpdateCameraComponents(int width, int height, SizeF letterSize)
    {
        foreach (var camera in ShadowCameras)
        {
            RecalulateShadowCameraInfo(camera, letterSize, width, height);
        }

        foreach (var camera in ActiveCameras)
        {

            RecalulateActiveCameraInfo(camera, letterSize, width, height);
        }
    }

    private void RecalulateShadowCameraInfo(UICamera shadowCamera, SizeF letterSize, float canvasWidth, float canvasHeight)
    {
        shadowCamera.RenderSpaceSize = new Vector2(canvasWidth, canvasHeight);
        shadowCamera.LetterSize = letterSize;
    }

    private void RecalulateActiveCameraInfo(UICamera activeCamera, SizeF letterSize, float canvasWidth, float canvasHeight)
    {
        // TODO Calculate letterSize for UI Space
        activeCamera.LetterSize = new SizeF(letterSize.Width / canvasWidth, letterSize.Height / canvasHeight);
    }

}
