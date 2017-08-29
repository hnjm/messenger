using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mikodev.Network
{
    public enum LinkError : int
    {
        None,
        Overflow,
        AssertFailed,
        CodeConflict,
        CodeInvalid,
        Shutdown,
        Success,
        CountLimited,
        ProtocolMismatch,
    }
}
