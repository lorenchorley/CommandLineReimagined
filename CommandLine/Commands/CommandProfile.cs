namespace CommandLineReimagine.Commands
{
    public record CommandProfile(
        string Name,
        string Description,
        CommandParameter[] Parameters,
        Type CommandActionType
        );
}
