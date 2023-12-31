﻿using System;
using System.Windows.Controls;

namespace Application.FrameworkAccessors;

public class CanvasAccessor : FrameworkElementAccessor<Canvas, Image>
{
    public Canvas Canvas
    {
        get
        {
            ArgumentNullException.ThrowIfNull(Value1, nameof(Value1));

            return Value1;
        }
    }

    public Image CanvasImage
    {
        get
        {
            ArgumentNullException.ThrowIfNull(Value2, nameof(Value2));

            return Value2;
        }
    }
}

