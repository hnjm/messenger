namespace Mikodev.Network
{
    public enum LinkError : int
    {
        None,

        Success,

        Overflow,

        ProtocolMismatch,

        CodeInvalid,

        CodeConflict,

        CountLimited,

        GroupLimited,

        QueueLimited,
    }
}
