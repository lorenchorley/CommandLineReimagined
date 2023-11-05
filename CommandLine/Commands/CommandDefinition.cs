namespace Commands
{
    public record CommandDefinition(
        string Name,
        string Description,
        string KeyWords,
        CommandParameter[] Parameters,
        Type CommandActionType
        );
}
