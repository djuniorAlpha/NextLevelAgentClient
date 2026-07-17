using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelAgentClient.Core.Modal
{
    public class LoginResponse
    {

        public bool _isValid { get; set; } = false;
        public int _sessionTime { get; set; } = 0;
    }
}
