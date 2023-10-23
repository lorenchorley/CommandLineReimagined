﻿using System.Windows.Controls;

namespace InteractionLogic;

public class InputAccessor : FrameworkElementAccessor<TextBox>
{
    public TextBox Input
    {
        get
        {
            ArgumentNullException.ThrowIfNull(Value, nameof(Value));

            return Value;
        }
    }
}

