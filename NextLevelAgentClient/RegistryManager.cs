using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NextLevelAgentClient
{
    public static class RegistryManager
    {
        private const string REGISTRY_PATH = @"Software\Microsoft\Windows\CurrentVersion\Policies\System";
        private const string VALUE_NAME = "DisableTaskMgr";

        public static void LockManagerTask() {
            try
            {
                using RegistryKey key = Registry.CurrentUser.CreateSubKey(REGISTRY_PATH);
                if (key != null)
                {
                    key.SetValue(VALUE_NAME, 1, RegistryValueKind.DWord);
                }
            }
            catch (UnauthorizedAccessException)
            {
                System.Windows.Forms.MessageBox.Show(
                    "Erro: O Agente precisa de privilégios de Administrador para bloquear o Gerenciador de Tarefas.",
                    "Erro de Privilégios",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error
                );
            }
            catch (Exception ex) {
                Console.WriteLine($"Erro ao acessar o registro: {ex.Message}");
            }
        }

        public static void UnlockManagerTask() {
            try
            {
                using RegistryKey key = Registry.CurrentUser.OpenSubKey(REGISTRY_PATH, true);
                if(key != null) {
                    key.DeleteValue(VALUE_NAME, false);
                }
            } catch (Exception ex) {
                Console.WriteLine($"Erro ao liberar o registro: {ex.Message}");
            }
        }
    }
}
