namespace InteractionLogic;

public interface ITextUpdateSystem
{
    void InsertTextAtCursor(string command);
    void ClearText();
    void SetClipboardText(string path);
}
