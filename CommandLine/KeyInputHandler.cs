using CommandLine.Modules;
using Controller;
using EntityComponentSystem;
using EntityComponentSystem.Attributes;
using EntityComponentSystem.RayCasting;
using Interaction.Entities;
using InteractionLogic;

namespace Terminal;

public class KeyInputHandler : InputComponent
{
    [Inject] public RayCaster RayCaster { get; set; } = null!;
    [Inject] public LoopController LoopController { get; set; } = null!;
    [Inject] public Prompt Prompt { get; set; } = null!;
    [Inject] public ITextUpdateSystem TextSystem { get; set; } = null!;
    [Inject] public Shell Shell { get; set; } = null!;

    public int SelectionStart { get; private set; }
    public int SelectionLength { get; private set; }
    public string Text { get; private set; } = null!;

    public void SetCursorPosition(int selectionStart, int selectionLength)
    {
        // S'il y a besoin de rafraîchir l'affichage à cause d'une modification de la sélection
        if (SelectionStart != selectionStart ||
            SelectionLength != selectionLength)
            LoopController.RequestLoop();

        SelectionStart = selectionStart;
        SelectionLength = selectionLength;
    }

    public override void OnTextChanged(string text)
    {
        Prompt.SetPromptText(text);
    }

    public override void OnKeyDown(KeyEventInfo eventInfo)
    {
        if (eventInfo.Key == Key.Enter)
        {
            // Si maj est enfoncé, on va forcément à la ligne à la place d'exécuter la commande
            if (eventInfo.IsModifierPressed(ModifierKeys.Shift))
            {
                var previousStart = SelectionStart;

                // Insérer le retour à la ligne où il y a le curseur
                //var before = Text.Substring(0, previousStart);
                //var after = Text.Substring(previousStart);
                //_inputAccessor.Input.Text = before + "\n" + after;
                //_inputAccessor.Input.SelectionStart = previousStart + 1; // Pour se positionner sur la nouvelle ligne
                TextSystem.InsertTextAtCursor("\n");

                LoopController.RequestLoop();
                return;
            }

            // Sinon il faut qu'on détermine si la commande est exécutable pour continuer
            Shell.ExecuteCurrentPrompt();
            eventInfo.Handled = true; // TODO Needs mirroring logic in KeyDown to stop the text jumping up and then disappearing
            return;
        }

        if (eventInfo.Key == Key.Z &&
            eventInfo.IsModifierPressed(ModifierKeys.Control) &&
            eventInfo.IsModifierPressed(ModifierKeys.Shift))
        {
            // Undo is a command now, so the key press runs the same line a user could
            // type. That is what keeps the key and the word from drifting apart.
            Shell.ExecuteLine("undo");
            eventInfo.Handled = true;
            LoopController.RequestLoop();
            return;
        }
    }
}
