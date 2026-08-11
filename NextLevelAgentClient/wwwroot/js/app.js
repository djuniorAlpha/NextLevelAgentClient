(() => {
  const chromeHost = window.chrome && window.chrome.webview;

  const panels = {
    InitialBlocked: document.getElementById("panel-blocked"),
    TimeSelection: document.getElementById("panel-time"),
    WaitingForPix: document.getElementById("panel-pix"),
    Login: document.getElementById("panel-login"),
  };

  const btnBack = document.getElementById("btnBack");
  const statusChip = document.getElementById("statusChip");
  const statusText = document.getElementById("statusText");
  const pixCounter = document.getElementById("pixCounter");
  const machineNumberEl = document.getElementById("machineNumber");

  const alertOverlay = document.getElementById("alertOverlay");
  const alertIcon = document.getElementById("alertIcon");
  const alertTitle = document.getElementById("alertTitle");
  const alertMessage = document.getElementById("alertMessage");
  const alertOk = document.getElementById("alertOk");
  const envVersion = document.getElementById("envVersion");

  const statusClassByColor = {
    danger: "status-danger",
    success: "status-success",
    warning: "status-warning",
  };

  const alertIconByType = {
    error: "⛔",
    warning: "⚠️",
    success: "✅",
    info: "ℹ️",
  };

  function send(action, extra) {
    if (!chromeHost) return;
    chromeHost.postMessage(JSON.stringify({ action, ...extra }));
  }

  function showState(state) {
    Object.entries(panels).forEach(([key, panel]) => {
      panel?.classList.toggle("active", key === state);
    });
    btnBack.style.display = state !== "InitialBlocked" && state !== "ActiveSession" ? "block" : "none";
  }

  function setStatus(text, color) {
    statusText.textContent = text;
    statusChip.classList.remove("status-danger", "status-success", "status-warning");
    statusChip.classList.add(statusClassByColor[color] ?? "status-danger");
  }

  function setMachineNumber(number) {
    machineNumberEl.textContent = `Máquina Nº ${String(number).padStart(2, "0")}`;
  }

  function setEnvironment(value) {
    envVersion.textContent = `${value} • v1.0`;
    envVersion.classList.toggle("env-dev", value === "DEV");
  }

  function showAlert(alertType, title, message) {
    alertIcon.textContent = alertIconByType[alertType] ?? alertIconByType.info;
    alertIcon.className = `alert-icon alert-icon-${alertType}`;
    alertTitle.textContent = title;
    alertMessage.textContent = message;
    alertOverlay.classList.add("active");
  }

  alertOk.addEventListener("click", () => alertOverlay.classList.remove("active"));

  if (chromeHost) {
    chromeHost.addEventListener("message", (event) => {
      const msg = event.data;
      switch (msg.type) {
        case "state":
          showState(msg.state);
          break;
        case "status":
          setStatus(msg.text, msg.color);
          break;
        case "pixTick":
          pixCounter.textContent = msg.text;
          break;
        case "machineNumber":
          setMachineNumber(msg.number);
          break;
        case "environment":
          setEnvironment(msg.value);
          break;
        case "alert":
          showAlert(msg.alertType, msg.title, msg.text);
          break;
      }
    });
  }

  document.getElementById("btnBuyTime").addEventListener("click", () => send("buyTime"));
  document.getElementById("btnLogin").addEventListener("click", () => send("login"));
  document.getElementById("btnBack").addEventListener("click", () => send("back"));
  document.getElementById("btnSimulatePayment").addEventListener("click", () => send("simulatePayment"));

  document.querySelectorAll(".time-card").forEach((btn) => {
    btn.addEventListener("click", () => {
      send("selectTime", { minutes: parseInt(btn.dataset.minutes, 10) });
    });
  });

  document.getElementById("btnLoginRequest").addEventListener("click", () => {
    send("loginRequest", {
      username: document.getElementById("txtUsername").value,
      password: document.getElementById("txtPassword").value,
    });
  });
})();
