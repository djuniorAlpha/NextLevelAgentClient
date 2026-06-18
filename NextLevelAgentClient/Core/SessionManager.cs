using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelAgentClient.Core
{
    public class SessionManager
    {
        private readonly System.Windows.Forms.Timer _pixTimer;
        private readonly System.Windows.Forms.Timer _sessionTimer;

        public int RemainingPixTime { get; private set; }
        public int RemainingSessionTime { get; private set; }
        public MachineState CurrentState { get; private set; }

        public event Action<MachineState>? OnStateChanged;
        public event Action<TimeSpan>? OnPixTick;
        public event Action<TimeSpan>? OnSessionTick;
        public event Action? OnPixExpired;
        public event Action? OnSessionEnded;

        public SessionManager()
        {
            _pixTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _pixTimer.Tick += (s, e) => ProcessPixTick();

            _sessionTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _sessionTimer.Tick += (s, e) => ProcessSessionTick();

            CurrentState = MachineState.InitialBlocked;
        }

        public void ChangeState(MachineState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        public void StartPixExpectancy(int selectedMinutes)
        {
            RemainingSessionTime = selectedMinutes * 60;
            RemainingPixTime = 300; // 5 minutes timeout

            _pixTimer.Start();
            ChangeState(MachineState.WaitingForPix);
        }

        public void CancelOperation()
        {
            _pixTimer.Stop();
            _sessionTimer.Stop();
            ChangeState(MachineState.InitialBlocked);
        }

        public void ConfirmPixPayment()
        {
            _pixTimer.Stop();
            _sessionTimer.Start();
            ChangeState(MachineState.ActiveSession);
        }

        private void ProcessPixTick()
        {
            if (RemainingPixTime > 0)
            {
                RemainingPixTime--;
                OnPixTick?.Invoke(TimeSpan.FromSeconds(RemainingPixTime));
            }
            else
            {
                _pixTimer.Stop();
                ChangeState(MachineState.InitialBlocked);
                OnPixExpired?.Invoke();
            }
        }

        private void ProcessSessionTick()
        {
            if (RemainingSessionTime > 0)
            {
                RemainingSessionTime--;
                OnSessionTick?.Invoke(TimeSpan.FromSeconds(RemainingSessionTime));
            }
            else
            {
                _sessionTimer.Stop();
                ChangeState(MachineState.InitialBlocked);
                OnSessionEnded?.Invoke();
            }
        }
    }
}
