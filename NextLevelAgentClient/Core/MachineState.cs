using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelAgentClient.Core
{
    public enum MachineState
    {
        InitialBlocked,
        TimeSelection,
        WaitingForPix,
        ActiveSession
    }
}
