namespace Commands
{
    public record Command(
        string Name,
        string Description,
        CommandParameter[] Parameters,
        Type CommandActionType
        );
}
