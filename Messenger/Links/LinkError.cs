namespace Mikodev.Network
{
    public enum LinkError : int
    {
        None,

        Success,

        Overflow,

        AssertFailed,

        ProtocolMismatch,

        CodeInvalid,

        CodeConflict,

        CountLimited,

        GroupLimited,

        QueueLimited,
    }
}
