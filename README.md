# Next Level Agent Client

Aplicação desktop (Windows Forms + WebView2) que transforma um computador em uma estação de acesso pago para lan houses/gaming houses: bloqueia a máquina, exibe uma interface web de compra de tempo/login e libera o uso por um período controlado.

## Sobre o projeto

O **Next Level Agent Client** roda como um agente local em cada máquina da casa de jogos. Ao iniciar, ele:

1. Assume a tela inteira e bloqueia atalhos do sistema (Alt+Tab, Alt+F4, tecla Windows, Ctrl+Esc) e o Gerenciador de Tarefas.
2. Sincroniza o hardware da máquina com o backend (registro/consulta por endereço MAC).
3. Exibe uma interface (HTML/CSS/JS renderizada via WebView2) para o usuário comprar tempo via Pix ou fazer login.
4. Libera o acesso por um período controlado, minimiza para a bandeja do sistema e monitora o tempo restante.
5. Ao término da sessão, bloqueia a máquina novamente.

## Funcionalidades

- **Tela de bloqueio em tela cheia**, sempre no topo, sem bordas.
- **Bloqueio de atalhos do sistema** via hook de teclado de baixo nível (`KeyboardHook`).
- **Bloqueio do Gerenciador de Tarefas** via registro do Windows (`RegistryManager`), com fallback caso a aplicação não tenha privilégios de Administrador.
- **Fluxo de estados da sessão** (`SessionManager`/`MachineState`): bloqueado → seleção de tempo → aguardando Pix → sessão ativa → login.
- **Compra de tempo via Pix simulado**, com contagem regressiva e expiração automática.
- **Login manual** (usuário/senha) como via alternativa de liberação.
- **Ícone na bandeja do sistema** com notificações de tempo restante e sessão liberada/encerrada.
- **Atalho de saída para desenvolvedor** (Shift+F12) que libera o bloqueio e encerra a aplicação.
- **Interface web local** (`wwwroot/`) servida via WebView2 (`https://appassets/index.html`), comunicando-se com o backend .NET por mensagens (`postMessage`).
- **Configuração por ambiente** (`appsettings.json` + `AppConfig`/`AppEnvironment`), definindo `DEV`/`PROD` e a URL base do backend.

## Estrutura do projeto

```
NextLevelAgentClient/
├── Core/
│   ├── AppConfig.cs            # Leitura de appsettings.json (ambiente e URL do backend)
│   ├── AppEnvironment.cs       # Enum Dev/Prod
│   ├── MachineState.cs         # Estados possíveis da máquina/sessão
│   ├── RegistryManager.cs      # Bloqueio/desbloqueio do Gerenciador de Tarefas
│   ├── SessionManager.cs       # Máquina de estados e timers de sessão/Pix
│   └── Services/
│       ├── IComputerApiService.cs      # Contrato de integração com o backend
│       ├── MachineRegistration.cs      # DTO de registro da máquina
│       └── MockComputerApiService.cs   # Implementação simulada (sem backend real ainda)
├── Infrastructure/
│   └── KeyboardHook.cs         # Hook global de teclado (bloqueia atalhos do Windows)
├── Presentation/
│   ├── LockForm.cs             # Form principal: WebView2, bandeja, orquestração da UI
│   ├── LockForm.Designer.cs
│   └── LockForm.resx
├── wwwroot/                    # Interface web (HTML/CSS/JS) exibida no WebView2
│   ├── index.html
│   ├── css/style.css
│   ├── js/app.js
│   └── assets/ (ícones)
├── appsettings.json             # Configuração de ambiente e URL do backend
├── Program.cs                   # Entry point (WinForms)
└── NextLevelAgentClient.csproj
```

## Como funciona o fluxo de estados

```
InitialBlocked ──"Comprar tempo"──▶ TimeSelection ──seleciona minutos──▶ WaitingForPix
      │                                                                      │
      └──────────"Login"──▶ Login ──credenciais válidas──▶ ActiveSession ◀──┘ (Pix confirmado)
                                                                  │
                                                     tempo esgotado / sessão encerrada
                                                                  ▼
                                                           InitialBlocked
```

A comunicação entre a interface web (`wwwroot/js/app.js`) e o backend .NET (`LockForm.cs`) acontece via `WebMessage` (WebView2), com ações como `buyTime`, `login`, `loginRequest`, `selectTime`, `simulatePayment` e `back`.

## Backend

> **Importante:** este projeto ainda não possui um backend real integrado. A implementação atual (`MockComputerApiService`) **simula** as respostas de registro de máquina, consulta e heartbeat, incluindo delays de rede artificiais. Esses mocks são um placeholder intencional para permitir o desenvolvimento e teste da UI enquanto o backend não está pronto — não é dívida técnica a ser "corrigida" às pressas, e sim o ponto de extensão previsto (`IComputerApiService`) para quando a integração real for implementada.

A URL do backend e o ambiente (`DEV`/`PROD`) são configurados em `appsettings.json`:

```json
{
  "Environment": "DEV",
  "BackendBaseUrl": "https://localhost:5001/api"
}
```

## Pré-requisitos

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (o projeto usa `net8.0-windows` e Windows Forms)
- [Evergreen Bootstrapper do WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) instalado (normalmente já presente em máquinas Windows atualizadas)

## Como executar

```bash
# Restaurar dependências
dotnet restore NextLevelAgentClient.sln

# Rodar em modo debug
dotnet run --project NextLevelAgentClient/NextLevelAgentClient.csproj
```

Ou abra `NextLevelAgentClient.sln` no Visual Studio 2022+ e pressione F5.

> **Atenção:** a aplicação assume a tela e bloqueia atalhos globais do sistema (Alt+Tab, tecla Windows, Gerenciador de Tarefas, etc.). Para sair durante o desenvolvimento, use **Shift+F12**, que aciona a saída de desenvolvedor e libera todos os bloqueios antes de encerrar o processo.

## Credenciais de teste

Enquanto o backend real não existe, o login aceita apenas as credenciais fixas:

- **Usuário:** `admin`
- **Senha:** `admin`

## Licença

Projeto privado / uso interno.
