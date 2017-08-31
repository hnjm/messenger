namespace Mikodev.Network
{
    public enum LinkError : int
    {
        None,

        Overflow,

        AssertFailed,

        CodeConflict,

        CodeInvalid,

        Success,

        CountLimited,

        ProtocolMismatch,

        GroupLimited,
    }
}
