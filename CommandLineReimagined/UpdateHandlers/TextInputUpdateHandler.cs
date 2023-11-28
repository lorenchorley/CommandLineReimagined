using Application.FrameworkAccessors;
using EntityComponentSystem;
using InteractionLogic;
using System.Windows;

namespace Application.UpdateHandlers;

public class TextInputUpdateHandler : IECSSystem, ITextUpdateSystem
{
    private readonly InputAccessor _inputAccessor;

    public TextInputUpdateHandler(InputAccessor inputAccessor)
    {
        _inputAccessor = inputAccessor;
    }

    public void OnInit()
    {
    }

    public void OnStart()
    {
    }

    public void ClearText()
    {
        _inputAccessor.Input.Text = "";
    }

    public void SetClipboardText(string path)
    {
        Clipboard.SetText(path);
    }

    public void InsertTextAtCursor(string command)
    {
        //var before = Text.Substring(0, previousStart);
        //var after = Text.Substring(previousStart);
        //_inputAccessor.Input.Text = before + "\n" + after;
        //_inputAccessor.Input.SelectionStart = previousStart + 1; // Pour se positionner sur la nouvelle ligne

        // S'il y a déjà une sélection, on l'ajoute à la fin
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
