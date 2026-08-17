using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelAgentClient.Core
{
    public static class RegistryManager
    {
        private const string REGISTRY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string VALUE_NAME = "DisableTaskMgr";

        /// <summary>
        /// Tenta bloquear o Gerenciador de Tarefas via registro.
        /// </summary>
        /// <returns>false se faltarem privilégios de Administrador; true caso contrário.</returns>
        public static bool LockManagerTask() {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH);
                key?.SetValue(VALUE_NAME, 1, RegistryValueKind.DWord);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (Exception ex) {
                Console.WriteLine($"Erro ao acessar o registro: {ex.Message}");
                return true;
            }
        }

        public static void UnlockManagerTask()
        {
            try
            {
                using RegistryKey? key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH, true);
                key?.DeleteValue(VALUE_NAME, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao liberar o registro: {ex.Message}");
            }
        }
    }
}
