using InteractionLogic.FrameworkAccessors;

namespace InteractionLogic;

public class TextInputUpdateHandler
{
    private readonly InputAccessor _inputAccessor;

    public TextInputUpdateHandler(InputAccessor inputAccessor)
    {
        _inputAccessor = inputAccessor;
    }

    public void InsertTextAtCursor(string command)
    {
        // S'il y a déjà une sélection, on l'ajout à la fin
        if (_inputAccessor.Input.SelectionLength > 0)
        {
            _inputAccessor.Input.SelectionStart = _inputAccessor.Input.SelectionStart + _inputAccessor.Input.SelectionLength;
            _inputAccessor.Input.SelectionLength = 0;
        }

        string inputText = _inputAccessor.Input.Text;
        int cursorPos = _inputAccessor.Input.CaretIndex;
        _inputAccessor.Input.Text = inputText.Insert(cursorPos, command);
        _inputAccessor.Input.CaretIndex = cursorPos;
        _inputAccessor.Input.SelectionStart = cursorPos;
        _inputAccessor.Input.SelectionLength = command.Length;
    }
}
