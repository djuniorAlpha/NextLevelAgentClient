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

        public void ConfirmLogin(int allocatedSeconds)
        {
            RemainingSessionTime = allocatedSeconds;
            _sessionTimer.Start();
            ChangeState(MachineState.ActiveSession);
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

        /// <summary>
        /// Libera a estação sem cobrança e sem tempo determinado (force-unlock do admin) -
        /// fica em sessão ativa "aberta" até o admin bloquear de novo.
        /// </summary>
        public void ForceUnlock()
        {
            _pixTimer.Stop();
            _sessionTimer.Stop();
            RemainingSessionTime = 0;
            ChangeState(MachineState.ActiveSession);
        }

        public void ConfirmPixPayment()
        {
            _pixTimer.Stop();
            _sessionTimer.Start();
            ChangeState(MachineState.ActiveSession);
        }

        /// <summary>
        /// Soma tempo a uma sessão já ativa (ex.: cliente comprou mais tempo pelo painel),
        /// sem reiniciar o timer nem sair de ActiveSession.
        /// </summary>
        public void ExtendSession(int additionalSeconds)
        {
            RemainingSessionTime += additionalSeconds;
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
