const API_KEY_STORAGE = "claudeTaskScheduler.apiKey";

function getApiKey() {
  return localStorage.getItem(API_KEY_STORAGE) || "";
}

function setApiKey(key) {
  localStorage.setItem(API_KEY_STORAGE, key);
}

async function apiFetch(path, options = {}) {
  const headers = Object.assign({}, options.headers, {
    "X-Api-Key": getApiKey(),
    "Content-Type": "application/json",
  });
  const response = await fetch(path, Object.assign({}, options, { headers }));
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `Request failed (${response.status})`);
  }
  if (response.status === 204) {
    return null;
  }
  return response.json();
}

function promptForApiKeyIfMissing() {
  if (!getApiKey()) {
    const key = window.prompt("Enter the API key shown in the Claude Task Scheduler tray icon:");
    if (key) {
      setApiKey(key.trim());
    }
  }
}

document.getElementById("key-btn").addEventListener("click", () => {
  const key = window.prompt("API key:", getApiKey());
  if (key !== null) {
    setApiKey(key.trim());
    loadTasks();
  }
});

const scheduleTypeSelect = document.getElementById("schedule-type");
const onceDailyFields = document.getElementById("once-daily-fields");
const weeklyFields = document.getElementById("weekly-fields");
const intervalFields = document.getElementById("interval-fields");

function updateScheduleFields() {
  const type = scheduleTypeSelect.value;
  weeklyFields.classList.toggle("hidden", type !== "Weekly");
  intervalFields.classList.toggle("hidden", type !== "Interval");
  onceDailyFields.classList.toggle("hidden", type === "Interval");

  const dateField = onceDailyFields.querySelector("input[name=date]").parentElement;
  const isOnce = type === "Once";
  onceDailyFields.querySelector("input[name=date]").required = isOnce;
  dateField.style.display = isOnce ? "" : "none";
}

scheduleTypeSelect.addEventListener("change", updateScheduleFields);
updateScheduleFields();

document.getElementById("create-form").addEventListener("submit", async (event) => {
  event.preventDefault();
  const errorEl = document.getElementById("create-error");
  errorEl.textContent = "";

  const form = event.target;
  const formData = new FormData(form);
  const scheduleType = formData.get("scheduleType");
  const time = formData.get("time") || "00:00";
  const date = formData.get("date");

  let startTime;
  if (scheduleType === "Once") {
    if (!date) {
      errorEl.textContent = "Date is required for a one-time task.";
      return;
    }
    startTime = new Date(`${date}T${time}:00`).toISOString();
  } else {
    const today = new Date();
    const [hours, minutes] = time.split(":");
    today.setHours(Number(hours), Number(minutes), 0, 0);
    startTime = today.toISOString();
  }

  const payload = {
    name: formData.get("name"),
    prompt: formData.get("prompt"),
    workingDirectory: formData.get("workingDirectory") || null,
    scheduleType,
    startTime,
    intervalMinutes: scheduleType === "Interval" ? Number(formData.get("intervalMinutes")) : null,
    daysOfWeek: scheduleType === "Weekly" ? formData.getAll("days") : null,
  };

  try {
    await apiFetch("/api/tasks", { method: "POST", body: JSON.stringify(payload) });
    form.reset();
    updateScheduleFields();
    await loadTasks();
  } catch (err) {
    errorEl.textContent = err.message;
  }
});

document.getElementById("refresh-btn").addEventListener("click", loadTasks);

async function loadTasks() {
  const list = document.getElementById("task-list");
  try {
    const tasks = await apiFetch("/api/tasks");
    renderTasks(tasks || []);
  } catch (err) {
    list.innerHTML = `<li class="error">${escapeHtml(err.message)}</li>`;
  }
}

function describeSchedule(task) {
  const time = new Date(task.startTime);
  const timeLabel = isNaN(time) ? "" : time.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  switch (task.scheduleType) {
    case "Once":
      return `Once at ${time.toLocaleString()}`;
    case "Daily":
      return `Daily at ${timeLabel}`;
    case "Weekly":
      return `Weekly (${(task.daysOfWeek || []).join(", ")}) at ${timeLabel}`;
    case "Interval":
      return `Every ${task.intervalMinutes} min`;
    default:
      return task.scheduleType;
  }
}

function renderTasks(tasks) {
  const list = document.getElementById("task-list");
  if (tasks.length === 0) {
    list.innerHTML = "<li>No scheduled tasks yet.</li>";
    return;
  }

  list.innerHTML = "";
  for (const task of tasks) {
    const li = document.createElement("li");
    li.className = "task-card";

    const statusLine = task.enabled ? "Enabled" : "Disabled";
    const lastRun = task.lastTriggeredUtc
      ? ` &middot; last run ${new Date(task.lastTriggeredUtc).toLocaleString()}`
      : "";

    li.innerHTML = `
      <h3>${escapeHtml(task.name)}</h3>
      <p>${escapeHtml(describeSchedule(task))}</p>
      <p>${statusLine}${lastRun}</p>
      <div class="task-actions">
        <button data-action="run">Run now</button>
        <button data-action="toggle">${task.enabled ? "Disable" : "Enable"}</button>
        <button data-action="log">View log</button>
        <button data-action="delete">Delete</button>
      </div>
    `;

    li.querySelector('[data-action="run"]').addEventListener("click", () => runTask(task.id));
    li.querySelector('[data-action="toggle"]').addEventListener("click", () => toggleTask(task.id, !task.enabled));
    li.querySelector('[data-action="log"]').addEventListener("click", () => showLog(task.id));
    li.querySelector('[data-action="delete"]').addEventListener("click", () => deleteTask(task.id));

    list.appendChild(li);
  }
}

function escapeHtml(value) {
  const div = document.createElement("div");
  div.textContent = value ?? "";
  return div.innerHTML;
}

async function runTask(id) {
  try {
    await apiFetch(`/api/tasks/${id}/run`, { method: "POST" });
    await loadTasks();
  } catch (err) {
    alert(err.message);
  }
}

async function toggleTask(id, enabled) {
  try {
    await apiFetch(`/api/tasks/${id}/toggle`, { method: "POST", body: JSON.stringify({ enabled }) });
    await loadTasks();
  } catch (err) {
    alert(err.message);
  }
}

async function deleteTask(id) {
  if (!confirm("Delete this scheduled task?")) {
    return;
  }
  try {
    await apiFetch(`/api/tasks/${id}`, { method: "DELETE" });
    await loadTasks();
  } catch (err) {
    alert(err.message);
  }
}

async function showLog(id) {
  const section = document.getElementById("log-section");
  const output = document.getElementById("log-output");
  try {
    const result = await apiFetch(`/api/tasks/${id}/log`);
    output.textContent = result.log || "(no log output yet)";
    section.classList.remove("hidden");
  } catch (err) {
    alert(err.message);
  }
}

document.getElementById("close-log-btn").addEventListener("click", () => {
  document.getElementById("log-section").classList.add("hidden");
});

promptForApiKeyIfMissing();
loadTasks();
