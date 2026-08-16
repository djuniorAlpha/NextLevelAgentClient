(() => {
  const chromeHost = window.chrome && window.chrome.webview;

  const panels = {
    InitialBlocked: document.getElementById("panel-blocked"),
    TimeSelection: document.getElementById("panel-time"),
    WaitingForPix: document.getElementById("panel-pix"),
    Login: document.getElementById("panel-login"),
    ChangePassword: document.getElementById("panel-change-password"),
    RedeemToken: document.getElementById("panel-redeem-token"),
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

  const pixQrPlaceholderEl = document.getElementById("pixQrPlaceholder");
  const pixQrImageEl = document.getElementById("pixQrImage");
  const pixAmountEl = document.getElementById("pixAmount");
  const btnCopyPixCode = document.getElementById("btnCopyPixCode");

  const timeOptionsEl = document.getElementById("timeOptions");
  const timeOptionsEmptyEl = document.getElementById("timeOptionsEmpty");
  const hourlyRateBoxEl = document.getElementById("hourlyRateBox");
  const hourlyRateLabelEl = document.getElementById("hourlyRateLabel");
  const hourlyHoursEl = document.getElementById("hourlyHours");
  const hourlyPriceEl = document.getElementById("hourlyPrice");
  const hourlyDecreaseBtn = document.getElementById("hourlyDecrease");
  const hourlyIncreaseBtn = document.getElementById("hourlyIncrease");
  const btnConfirmHourly = document.getElementById("btnConfirmHourly");

  const txtUsername = document.getElementById("txtUsername");
  const txtPassword = document.getElementById("txtPassword");
  const txtNewPassword = document.getElementById("txtNewPassword");
  const txtConfirmPassword = document.getElementById("txtConfirmPassword");
  const txtRedeemCode = document.getElementById("txtRedeemCode");

  const MAX_HOURLY_HOURS = 12;
  let selectedHourlyRate = null;
  let hourlyHours = 1;

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

  function formatCurrency(cents) {
    return `R$ ${(cents / 100).toFixed(2).replace(".", ",")}`;
  }

  function renderTimePackages(packages) {
    timeOptionsEl.innerHTML = "";
    packages.forEach((pkg) => {
      const btn = document.createElement("button");
      btn.className = "time-card";
      btn.type = "button";
      btn.dataset.minutes = pkg.minutes;
      btn.innerHTML = `
        <span class="time-card-duration">${pkg.minutes} min</span>
        <span class="time-card-label">${pkg.label}</span>
        <span class="time-card-price">${formatCurrency(pkg.priceCents)}</span>
      `;
      btn.addEventListener("click", () => send("selectTime", { minutes: pkg.minutes, kind: "package", optionId: pkg.id }));
      timeOptionsEl.appendChild(btn);
    });
  }

  function updateHourlyDisplay() {
    if (!selectedHourlyRate) return;
    hourlyHoursEl.textContent = `${hourlyHours}h`;
    hourlyPriceEl.textContent = formatCurrency(selectedHourlyRate.ratePerHourCents * hourlyHours);
  }

  function renderHourlyRate(hourlyRates) {
    selectedHourlyRate = hourlyRates.length > 0 ? hourlyRates[0] : null;
    hourlyRateBoxEl.classList.toggle("active", Boolean(selectedHourlyRate));

    if (selectedHourlyRate) {
      hourlyHours = 1;
      hourlyRateLabelEl.textContent = selectedHourlyRate.label;
      updateHourlyDisplay();
    }
  }

  function renderPricingOptions(packages, hourlyRates) {
    renderTimePackages(packages);
    renderHourlyRate(hourlyRates);
    timeOptionsEmptyEl.style.display = packages.length === 0 && hourlyRates.length === 0 ? "block" : "none";
  }

  hourlyDecreaseBtn.addEventListener("click", () => {
    if (hourlyHours > 1) {
      hourlyHours--;
      updateHourlyDisplay();
    }
  });

  hourlyIncreaseBtn.addEventListener("click", () => {
    if (hourlyHours < MAX_HOURLY_HOURS) {
      hourlyHours++;
      updateHourlyDisplay();
    }
  });

  btnConfirmHourly.addEventListener("click", () => {
    if (!selectedHourlyRate) return;
    send("selectTime", { minutes: hourlyHours * 60, kind: "hourly", optionId: selectedHourlyRate.id });
  });

  function resetLoginForm() {
    txtUsername.value = "";
    txtPassword.value = "";
    txtNewPassword.value = "";
    txtConfirmPassword.value = "";
    txtRedeemCode.value = "";
  }

  function resetPixPanel() {
    pixQrImageEl.style.display = "none";
    pixQrImageEl.src = "";
    pixQrPlaceholderEl.style.display = "block";
    pixAmountEl.textContent = "";
    btnCopyPixCode.style.display = "none";
    btnCopyPixCode.textContent = "📋 Copiar código Pix";
    delete btnCopyPixCode.dataset.pixCode;
  }

  function setPixData(qrCodeBase64, qrCodeText, amountCents) {
    if (qrCodeBase64) {
      pixQrImageEl.src = `data:image/png;base64,${qrCodeBase64}`;
      pixQrImageEl.style.display = "block";
      pixQrPlaceholderEl.style.display = "none";
    }
    if (typeof amountCents === "number") {
      pixAmountEl.textContent = formatCurrency(amountCents);
    }
    if (qrCodeText) {
      btnCopyPixCode.style.display = "block";
      btnCopyPixCode.dataset.pixCode = qrCodeText;
    }
  }

  btnCopyPixCode.addEventListener("click", async () => {
    const code = btnCopyPixCode.dataset.pixCode;
    if (!code) return;
    try {
      await navigator.clipboard.writeText(code);
      btnCopyPixCode.textContent = "✅ Copiado!";
      setTimeout(() => { btnCopyPixCode.textContent = "📋 Copiar código Pix"; }, 2000);
    } catch {
      // Clipboard API pode não estar disponível; ignora silenciosamente.
    }
  });

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
          if (msg.state === "WaitingForPix") resetPixPanel();
          if (msg.state === "InitialBlocked") resetLoginForm();
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
        case "pricingOptions":
          renderPricingOptions(msg.packages ?? [], msg.hourlyRates ?? []);
          break;
        case "pixData":
          setPixData(msg.qrCodeBase64, msg.qrCodeText, msg.amountCents);
          break;
      }
    });
  }

  document.getElementById("btnBuyTime").addEventListener("click", () => send("buyTime"));
  document.getElementById("btnLogin").addEventListener("click", () => send("login"));
  document.getElementById("btnRedeemToken").addEventListener("click", () => send("redeemToken"));
  document.getElementById("btnBack").addEventListener("click", () => send("back"));

  txtRedeemCode.addEventListener("input", () => {
    txtRedeemCode.value = txtRedeemCode.value.toUpperCase();
  });

  document.getElementById("btnRedeemTokenRequest").addEventListener("click", () => {
    send("redeemTokenRequest", { code: txtRedeemCode.value });
  });

  document.getElementById("btnLoginRequest").addEventListener("click", () => {
    send("loginRequest", {
      username: txtUsername.value,
      password: txtPassword.value,
    });
  });

  document.getElementById("btnChangePasswordRequest").addEventListener("click", () => {
    send("changePasswordRequest", {
      newPassword: txtNewPassword.value,
      confirmPassword: txtConfirmPassword.value,
    });
  });
})();
