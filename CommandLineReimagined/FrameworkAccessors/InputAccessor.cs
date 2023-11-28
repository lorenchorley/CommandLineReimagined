using System;
using System.Windows.Controls;

namespace Application.FrameworkAccessors;

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

