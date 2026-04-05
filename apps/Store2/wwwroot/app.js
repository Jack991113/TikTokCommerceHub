const dom = {
  configForm: document.getElementById("configForm"),
  saveConfigButton: document.getElementById("saveConfigButton"),
  pollButton: document.getElementById("pollButton"),
  refreshTokenButton: document.getElementById("refreshTokenButton"),
  refreshWorkspaceButton: document.getElementById("refreshWorkspaceButton"),
  printSelectedButton: document.getElementById("printSelectedButton"),
  exchangeTokenButton: document.getElementById("exchangeTokenButton"),
  historySearchButton: document.getElementById("historySearchButton"),
  historyPrevButton: document.getElementById("historyPrevButton"),
  historyNextButton: document.getElementById("historyNextButton"),
  refreshActiveDatasetButton: document.getElementById("refreshActiveDatasetButton"),
  selectVisibleButton: document.getElementById("selectVisibleButton"),
  clearSelectedButton: document.getElementById("clearSelectedButton"),
  selectionBar: document.getElementById("selectionBar"),
  selectionHint: document.getElementById("selectionHint"),
  selectionPrintButton: document.getElementById("selectionPrintButton"),
  selectionClearButton: document.getElementById("selectionClearButton"),
  pollStateSignal: document.getElementById("pollStateSignal"),
  bridgeStateSignal: document.getElementById("bridgeStateSignal"),
  configStateSignal: document.getElementById("configStateSignal"),
  activeTabPill: document.getElementById("activeTabPill"),
  orderList: document.getElementById("orderList"),
  previewPlaceholder: document.getElementById("previewPlaceholder"),
  previewContent: document.getElementById("previewContent"),
  contextMenu: document.getElementById("contextMenu"),
  toastStack: document.getElementById("toastStack"),
  printerNames: document.getElementById("printerNames"),
  printerChips: document.getElementById("printerChips"),
  historySummary: document.getElementById("historySummary"),
  statusGrid: document.getElementById("statusGrid"),
  messageBoard: document.getElementById("messageBoard"),
  metricCachedCount: document.getElementById("metricCachedCount"),
  metricFoundCount: document.getElementById("metricFoundCount"),
  metricPrintedCount: document.getElementById("metricPrintedCount"),
  metricBridgePendingCount: document.getElementById("metricBridgePendingCount"),
  metricBridgeMatchedCount: document.getElementById("metricBridgeMatchedCount"),
  metricSelectedCount: document.getElementById("metricSelectedCount"),
  deskMeta: document.getElementById("deskMeta"),
  shopBadge: document.getElementById("shopBadge"),
  printerBadge: document.getElementById("printerBadge"),
  selectedRawFieldCount: document.getElementById("selectedRawFieldCount"),
  listSearch: document.getElementById("listSearch"),
  listPrintState: document.getElementById("listPrintState"),
  authCodeOrUrl: document.getElementById("authCodeOrUrl"),
  storeName: document.getElementById("storeName"),
  printerName: document.getElementById("printerName"),
  paperSize: document.getElementById("paperSize"),
  customPaperWidthMm: document.getElementById("customPaperWidthMm"),
  customPaperHeightMm: document.getElementById("customPaperHeightMm"),
  marginMm: document.getElementById("marginMm"),
  paperWidthCharacters: document.getElementById("paperWidthCharacters"),
  baseFontSize: document.getElementById("baseFontSize"),
  minFontSize: document.getElementById("minFontSize"),
  autoPrintNewOrders: document.getElementById("autoPrintNewOrders"),
  autoPrintAfterBridgeCapture: document.getElementById("autoPrintAfterBridgeCapture"),
  appKey: document.getElementById("appKey"),
  appSecret: document.getElementById("appSecret"),
  shopId: document.getElementById("shopId"),
  accessToken: document.getElementById("accessToken"),
  refreshTokenInput: document.getElementById("refreshTokenInput"),
  showBuyerAccountName: document.getElementById("showBuyerAccountName"),
  showBuyerPlatformUserId: document.getElementById("showBuyerPlatformUserId"),
  showBuyerName: document.getElementById("showBuyerName"),
  showBuyerEmail: document.getElementById("showBuyerEmail"),
  showRecipientPhone: document.getElementById("showRecipientPhone"),
  showBuyerMessage: document.getElementById("showBuyerMessage"),
  showOrderAmounts: document.getElementById("showOrderAmounts"),
  showItemDetails: document.getElementById("showItemDetails"),
  showSku: document.getElementById("showSku"),
  showPaidTime: document.getElementById("showPaidTime"),
  showCreatedTime: document.getElementById("showCreatedTime"),
  historyFrom: document.getElementById("historyFrom"),
  historyTo: document.getElementById("historyTo"),
  historyPageSize: document.getElementById("historyPageSize"),
  historyStatus: document.getElementById("historyStatus"),
  historyKeyword: document.getElementById("historyKeyword"),
  tabButtons: [...document.querySelectorAll("[data-tab]")],
  viewButtons: [...document.querySelectorAll("[data-view-mode]")],
  rangeButtons: [...document.querySelectorAll("[data-range-days]")],
  contextButtons: [...document.querySelectorAll("[data-menu-action]")],
  navButtons: [...document.querySelectorAll("[data-scroll-target]")],
  customPaperFields: [...document.querySelectorAll("[data-custom-paper]")]
};

const state = {
  activeTab: "cache",
  orderView: "board",
  config: null,
  status: null,
  printers: [],
  cachedOrders: [],
  history: {
    queried: false,
    loading: false,
    orders: [],
    totalCount: 0,
    returnedCount: 0,
    pageTokens: [""],
    pageIndex: 0,
    nextPageToken: "",
    query: null
  },
  selectedIds: new Set(),
  selectedRawFieldPaths: new Set(),
  preview: {
    orderId: "",
    loading: false,
    data: null,
    fieldMode: "all",
    fieldSearch: "",
    tab: "overview"
  },
  contextOrder: null,
  dirtyConfig: false,
  suppressDirtyTracking: false,
  savedConfigSignature: "",
  configAutoSaveTimer: 0,
  configSaveInFlight: false,
  configSaveQueued: false,
  configSaveError: ""
};

const statusLabels = {
  UNPAID: "未支付",
  ON_HOLD: "挂起",
  AWAITING_SHIPMENT: "待发货",
  TO_SHIP: "待发货",
  DELIVERED: "已送达",
  COMPLETED: "已完成",
  CANCELLED: "已取消"
};

const previewTabs = {
  overview: "订单摘要",
  ticket: "票面预览",
  fields: "接口字段",
  json: "原始 JSON"
};

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function normalizeText(value) {
  return String(value ?? "").trim().toLowerCase();
}

function truncate(value, max = 32) {
  const text = String(value ?? "").trim();
  if (!text) {
    return "";
  }

  return text.length > max ? `${text.slice(0, max - 1)}…` : text;
}

function parseDateValue(value) {
  if (!value) {
    return 0;
  }

  const timestamp = new Date(value).getTime();
  return Number.isNaN(timestamp) ? 0 : timestamp;
}

function formatDate(value) {
  if (!value) {
    return "—";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return date.toLocaleString("zh-CN", {
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    second: "2-digit"
  });
}

function formatDateShort(value) {
  if (!value) {
    return "—";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  return date.toLocaleString("zh-CN", {
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit"
  });
}

function formatMoney(amount, currency) {
  if (amount === null || amount === undefined || amount === "") {
    return "—";
  }

  return `${amount} ${currency || ""}`.trim();
}

function formatQuantityValue(value) {
  if (value === null || value === undefined || value === "") {
    return "0";
  }

  const numeric = Number(value);
  if (Number.isNaN(numeric)) {
    return String(value);
  }

  return Number.isInteger(numeric) ? String(numeric) : numeric.toFixed(2).replace(/\.?0+$/, "");
}

function emailAlias(value) {
  if (!value) {
    return "";
  }

  const index = String(value).indexOf("@");
  return index > 0 ? String(value).slice(0, index) : String(value);
}

function boolFromSelect(selectElement) {
  return selectElement.value === "true";
}

function numberOrNull(value, integer = false) {
  if (value === null || value === undefined || value === "") {
    return null;
  }

  const parsed = integer ? Number.parseInt(value, 10) : Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : null;
}

function toLocalInputValue(value) {
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) {
    return "";
  }

  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  const hour = String(date.getHours()).padStart(2, "0");
  const minute = String(date.getMinutes()).padStart(2, "0");
  return `${year}-${month}-${day}T${hour}:${minute}`;
}

function fromLocalInputValue(value) {
  if (!value) {
    return null;
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? null : date.toISOString();
}

function translateStatus(value) {
  return statusLabels[value] || value || "未知";
}

function compareOrders(left, right) {
  const leftPrimary = parseDateValue(left.paidAtUtc) || parseDateValue(left.createdAtUtc) || parseDateValue(left.processedAtUtc) || parseDateValue(left.updatedAtUtc);
  const rightPrimary = parseDateValue(right.paidAtUtc) || parseDateValue(right.createdAtUtc) || parseDateValue(right.processedAtUtc) || parseDateValue(right.updatedAtUtc);

  if (rightPrimary !== leftPrimary) {
    return rightPrimary - leftPrimary;
  }

  return String(right.orderId || "").localeCompare(String(left.orderId || ""));
}

async function copyText(value, successMessage = "已复制到剪贴板。") {
  const text = String(value ?? "");
  if (!text.trim()) {
    showToast("没有可复制的内容。", "error");
    return;
  }

  try {
    await navigator.clipboard.writeText(text);
  } catch {
    const fallback = document.createElement("textarea");
    fallback.value = text;
    fallback.style.position = "fixed";
    fallback.style.opacity = "0";
    document.body.appendChild(fallback);
    fallback.focus();
    fallback.select();
    document.execCommand("copy");
    fallback.remove();
  }

  showToast(successMessage, "success");
}

function showToast(message, type = "info") {
  const toast = document.createElement("div");
  toast.className = `toast ${type}`;
  toast.textContent = message;
  dom.toastStack.appendChild(toast);
  window.setTimeout(() => toast.remove(), 3200);
}

function setMessage(message, type = "info") {
  dom.messageBoard.textContent = message;
  dom.messageBoard.classList.remove("is-success", "is-error");

  if (type === "success") {
    dom.messageBoard.classList.add("is-success");
  }

  if (type === "error") {
    dom.messageBoard.classList.add("is-error");
  }
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, options);
  const text = await response.text();
  let payload = {};

  if (text) {
    try {
      payload = JSON.parse(text);
    } catch {
      payload = {};
    }
  }

  if (!response.ok) {
    throw new Error(payload.message || payload.error || `请求失败：${response.status}`);
  }

  return payload;
}

function getConfigPayload() {
  return {
    storeName: dom.storeName.value.trim(),
    appKey: dom.appKey.value.trim(),
    appSecret: dom.appSecret.value.trim(),
    accessToken: dom.accessToken.value.trim(),
    refreshToken: dom.refreshTokenInput.value.trim(),
    shopId: dom.shopId.value.trim(),
    printerName: dom.printerName.value.trim(),
    paperSize: dom.paperSize.value,
    customPaperWidthMm: numberOrNull(dom.customPaperWidthMm.value),
    customPaperHeightMm: numberOrNull(dom.customPaperHeightMm.value),
    marginMm: numberOrNull(dom.marginMm.value),
    paperWidthCharacters: numberOrNull(dom.paperWidthCharacters.value, true),
    baseFontSize: numberOrNull(dom.baseFontSize.value),
    minFontSize: numberOrNull(dom.minFontSize.value),
    autoPrintNewOrders: boolFromSelect(dom.autoPrintNewOrders),
    autoPrintAfterBridgeCapture: boolFromSelect(dom.autoPrintAfterBridgeCapture),
    showBuyerAccountName: dom.showBuyerAccountName.checked,
    showBuyerPlatformUserId: dom.showBuyerPlatformUserId.checked,
    showBuyerName: dom.showBuyerName.checked,
    showBuyerEmail: dom.showBuyerEmail.checked,
    showRecipientPhone: dom.showRecipientPhone.checked,
    showBuyerMessage: dom.showBuyerMessage.checked,
    showOrderAmounts: dom.showOrderAmounts.checked,
    showItemDetails: dom.showItemDetails.checked,
    showSku: dom.showSku.checked,
    showPaidTime: dom.showPaidTime.checked,
    showCreatedTime: dom.showCreatedTime.checked,
    selectedRawFieldPaths: [...state.selectedRawFieldPaths]
  };
}

function normalizeConfigPayload(payload) {
  return {
    storeName: String(payload?.storeName || "").trim(),
    appKey: String(payload?.appKey || "").trim(),
    appSecret: String(payload?.appSecret || "").trim(),
    accessToken: String(payload?.accessToken || "").trim(),
    refreshToken: String(payload?.refreshToken || "").trim(),
    shopId: String(payload?.shopId || "").trim(),
    printerName: String(payload?.printerName || "").trim(),
    paperSize: String(payload?.paperSize || "100x150").trim(),
    customPaperWidthMm: payload?.customPaperWidthMm ?? null,
    customPaperHeightMm: payload?.customPaperHeightMm ?? null,
    marginMm: payload?.marginMm ?? null,
    paperWidthCharacters: payload?.paperWidthCharacters ?? null,
    baseFontSize: payload?.baseFontSize ?? null,
    minFontSize: payload?.minFontSize ?? null,
    autoPrintNewOrders: Boolean(payload?.autoPrintNewOrders),
    autoPrintAfterBridgeCapture: Boolean(payload?.autoPrintAfterBridgeCapture),
    showBuyerAccountName: Boolean(payload?.showBuyerAccountName),
    showBuyerPlatformUserId: Boolean(payload?.showBuyerPlatformUserId),
    showBuyerName: Boolean(payload?.showBuyerName),
    showBuyerEmail: Boolean(payload?.showBuyerEmail),
    showRecipientPhone: Boolean(payload?.showRecipientPhone),
    showBuyerMessage: Boolean(payload?.showBuyerMessage),
    showOrderAmounts: Boolean(payload?.showOrderAmounts),
    showItemDetails: Boolean(payload?.showItemDetails),
    showSku: Boolean(payload?.showSku),
    showPaidTime: Boolean(payload?.showPaidTime),
    showCreatedTime: Boolean(payload?.showCreatedTime),
    selectedRawFieldPaths: [...(payload?.selectedRawFieldPaths || [])]
      .map((item) => String(item || "").trim())
      .filter(Boolean)
      .sort((left, right) => left.localeCompare(right))
  };
}

function buildConfigSignature(payload) {
  return JSON.stringify(normalizeConfigPayload(payload));
}

function refreshDirtyState() {
  if (state.suppressDirtyTracking) {
    return;
  }

  state.dirtyConfig = buildConfigSignature(getConfigPayload()) !== state.savedConfigSignature;
  renderSignals();
}

function markConfigDirty(isDirty = true) {
  if (state.suppressDirtyTracking) {
    return;
  }

  if (!isDirty) {
    state.dirtyConfig = false;
    renderSignals();
    return;
  }

  refreshDirtyState();
}

function toggleCustomPaperFields() {
  const isCustom = dom.paperSize.value === "custom";
  dom.customPaperFields.forEach((element) => element.classList.toggle("hidden", !isCustom));
}

function fillConfig(config) {
  state.suppressDirtyTracking = true;

  state.config = config;
  dom.storeName.value = config.storeName || "";
  dom.appKey.value = config.appKey || "";
  dom.appSecret.value = config.appSecret || "";
  dom.accessToken.value = config.accessToken || "";
  dom.refreshTokenInput.value = config.refreshToken || "";
  dom.shopId.value = config.shopId || "";
  dom.printerName.value = config.printerName || "";
  dom.paperSize.value = config.paperSize || "100x150";
  dom.customPaperWidthMm.value = config.customPaperWidthMm ?? "";
  dom.customPaperHeightMm.value = config.customPaperHeightMm ?? "";
  dom.marginMm.value = config.marginMm ?? "";
  dom.paperWidthCharacters.value = config.paperWidthCharacters ?? "";
  dom.baseFontSize.value = config.baseFontSize ?? "";
  dom.minFontSize.value = config.minFontSize ?? "";
  dom.autoPrintNewOrders.value = String(Boolean(config.autoPrintNewOrders));
  dom.autoPrintAfterBridgeCapture.value = String(Boolean(config.autoPrintAfterBridgeCapture));
  dom.showBuyerAccountName.checked = Boolean(config.showBuyerAccountName);
  dom.showBuyerPlatformUserId.checked = Boolean(config.showBuyerPlatformUserId);
  dom.showBuyerName.checked = Boolean(config.showBuyerName);
  dom.showBuyerEmail.checked = Boolean(config.showBuyerEmail);
  dom.showRecipientPhone.checked = Boolean(config.showRecipientPhone);
  dom.showBuyerMessage.checked = Boolean(config.showBuyerMessage);
  dom.showOrderAmounts.checked = Boolean(config.showOrderAmounts);
  dom.showItemDetails.checked = Boolean(config.showItemDetails);
  dom.showSku.checked = Boolean(config.showSku);
  dom.showPaidTime.checked = Boolean(config.showPaidTime);
  dom.showCreatedTime.checked = Boolean(config.showCreatedTime);
  state.selectedRawFieldPaths = new Set(config.selectedRawFieldPaths || []);
  state.savedConfigSignature = buildConfigSignature(config);

  state.suppressDirtyTracking = false;
  state.dirtyConfig = false;

  toggleCustomPaperFields();
  updateSelectedRawFieldCount();
  updateHeaderBadges();
  renderSignals();
}

function updateSelectedRawFieldCount() {
  dom.selectedRawFieldCount.textContent = `${state.selectedRawFieldPaths.size} 项`;
}

function renderPrinters(printers) {
  state.printers = Array.isArray(printers) ? printers : [];
  dom.printerNames.innerHTML = state.printers
    .map((printer) => `<option value="${escapeHtml(printer)}"></option>`)
    .join("");

  dom.printerChips.innerHTML = state.printers.length
    ? state.printers.map((printer) => `<button type="button" class="chip-button" data-printer-chip="${escapeHtml(printer)}">${escapeHtml(printer)}</button>`).join("")
    : '<span class="status-pill">未检测到打印机</span>';
}

function setSignal(element, text, tone = "neutral") {
  element.textContent = text;
  element.className = "signal";
  if (tone === "ok") {
    element.classList.add("signal--ok");
  }
  if (tone === "warn") {
    element.classList.add("signal--warn");
  }
  if (tone === "error") {
    element.classList.add("signal--error");
  }
}

function getBridgeDerivedState() {
  const matchedCount = state.cachedOrders.filter((order) => Boolean(order.buyerAccountNameSource)).length;
  const waitingCount = state.cachedOrders.filter((order) =>
    Boolean(state.config?.autoPrintAfterBridgeCapture) &&
    !order.buyerAccountName &&
    !order.printedAtUtc
  ).length;

  return { matchedCount, waitingCount };
}

function getBridgeHeartbeatState() {
  const heartbeatAt = parseDateValue(state.status?.lastBridgeHeartbeatAtUtc);
  const now = Date.now();

  return {
    isOnline: heartbeatAt > 0 && now - heartbeatAt < 60000,
    heartbeatAt,
    captureAt: parseDateValue(state.status?.lastBridgeCaptureAtUtc),
    sourceUrl: state.status?.lastBridgeSourceUrl || "",
    orderId: state.status?.lastBridgeOrderId || "",
    buyerNickname: state.status?.lastBridgeBuyerNickname || ""
  };
}

function updateHeaderBadges() {
  dom.shopBadge.textContent = state.config?.shopId ? `店铺：${truncate(state.config.shopId, 24)}` : "未配置店铺";
  dom.printerBadge.textContent = state.config?.printerName ? `打印机：${truncate(state.config.printerName, 24)}` : "未选择打印机";
  dom.activeTabPill.textContent = state.activeTab === "history" ? "历史订单" : "缓存订单";
}

function renderSignals() {
  if (!state.status) {
    setSignal(dom.pollStateSignal, "服务连接中");
  } else if (state.status.lastError) {
    setSignal(dom.pollStateSignal, "轮询异常", "error");
  } else if (state.status.isPolling) {
    setSignal(dom.pollStateSignal, "正在轮询", "warn");
  } else {
    setSignal(dom.pollStateSignal, "轮询正常", "ok");
  }
  const { matchedCount, waitingCount } = getBridgeDerivedState();
  const bridgeHeartbeat = getBridgeHeartbeatState();
  if (bridgeHeartbeat.isOnline && bridgeHeartbeat.buyerNickname) {
    setSignal(dom.bridgeStateSignal, `桥接在线：${bridgeHeartbeat.buyerNickname}`, "ok");
  } else if (bridgeHeartbeat.isOnline) {
    setSignal(dom.bridgeStateSignal, "桥接在线，等待识别客户ID", "ok");
  } else if (waitingCount > 0) {
    setSignal(dom.bridgeStateSignal, `${waitingCount} 单等待客户ID`, "warn");
  } else if (matchedCount > 0) {
    setSignal(dom.bridgeStateSignal, `已桥接 ${matchedCount} 单`, "ok");
  } else {
    setSignal(dom.bridgeStateSignal, "桥接待机");
  }
  if (state.configSaveInFlight) {
    setSignal(dom.configStateSignal, "配置自动保存中", "warn");
  } else if (state.configSaveError) {
    setSignal(dom.configStateSignal, "配置自动保存失败", "error");
  } else if (state.dirtyConfig) {
    setSignal(dom.configStateSignal, "配置待自动保存", "warn");
  } else {
    setSignal(dom.configStateSignal, "配置已自动保存", "ok");
  }
}

function renderMetrics() {
  const { matchedCount, waitingCount } = getBridgeDerivedState();
  dom.metricCachedCount.textContent = String(state.cachedOrders.length);
  dom.metricFoundCount.textContent = String(state.status?.lastOrdersFound ?? 0);
  dom.metricPrintedCount.textContent = String(state.cachedOrders.filter((order) => Boolean(order.printedAtUtc)).length);
  dom.metricBridgePendingCount.textContent = String(waitingCount);
  dom.metricBridgeMatchedCount.textContent = String(matchedCount);
  dom.metricSelectedCount.textContent = String(state.selectedIds.size);
}

function renderStatus() {
  const status = state.status;
  if (!status) {
    dom.statusGrid.innerHTML = "";
    renderSignals();
    return;
  }

  const cards = [
    ["轮询状态", status.isPolling ? "正在处理订单" : "空闲"],
    ["最近轮询开始", formatDate(status.lastPollStartedAtUtc)],
    ["最近轮询结束", formatDate(status.lastPollCompletedAtUtc)],
    ["最近处理订单", formatDate(status.lastProcessedOrderAtUtc)],
    ["最近发现", `${status.lastOrdersFound ?? 0} 单`],
    ["最近打印", `${status.lastOrdersPrinted ?? 0} 单`]
  ];

  dom.statusGrid.innerHTML = cards.map(([label, value]) => `
    <article class="status-card">
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(value)}</strong>
    </article>
  `).join("");

  if (status.lastError) {
    setMessage(`最近错误：${status.lastError}`, "error");
  } else {
    setMessage(
      `轮询服务正常。当前缓存 ${state.cachedOrders.length} 单，最近一轮发现 ${status.lastOrdersFound ?? 0} 单，打印 ${status.lastOrdersPrinted ?? 0} 单。`,
      "success"
    );
  }

  renderSignals();
}

function renderHistorySummary() {
  if (!state.history.queried) {
    dom.historySummary.textContent = "尚未发起历史订单查询。";
    return;
  }

  const query = state.history.query;
  dom.historySummary.textContent = `范围：${formatDate(query.fromUtc)} 至 ${formatDate(query.toUtc)}，第 ${state.history.pageIndex + 1} 页，本页 ${state.history.returnedCount} 单，共 ${state.history.totalCount} 单。`;
}

function getActiveOrders() {
  return state.activeTab === "history" ? state.history.orders : state.cachedOrders;
}

function getOrderById(orderId) {
  return getActiveOrders().find((order) => order.orderId === orderId)
    || state.cachedOrders.find((order) => order.orderId === orderId)
    || state.history.orders.find((order) => order.orderId === orderId)
    || null;
}

function matchesQuickSearch(order, keyword) {
  if (!keyword) {
    return true;
  }

  const needle = normalizeText(keyword);
  return [
    order.orderId,
    order.displayName,
    order.primaryItemSummary,
    order.buyerAccountName,
    order.buyerPlatformUserId,
    order.buyerName,
    order.buyerEmail,
    order.recipientName,
    order.recipientPhone,
    order.recipientAddress,
    order.status
  ].some((value) => normalizeText(value).includes(needle));
}

function matchesPrintState(order, printState) {
  if (printState === "all") {
    return true;
  }

  if (printState === "printed") {
    return Boolean(order.printedAtUtc);
  }

  if (printState === "failed") {
    return Boolean(order.printError);
  }

  return !order.printedAtUtc;
}

function getVisibleOrders() {
  const keyword = dom.listSearch.value.trim();
  const printState = dom.listPrintState.value;
  return getActiveOrders()
    .slice()
    .sort(compareOrders)
    .filter((order) => matchesQuickSearch(order, keyword))
    .filter((order) => matchesPrintState(order, printState));
}

function isCancelledOrder(order) {
  return normalizeText(order?.status) === "cancelled";
}

function isPaidOrder(order) {
  return Boolean(order?.paidAtUtc);
}

function isAwaitingShipmentOrder(order) {
  const status = normalizeText(order?.status);
  return status === "awaiting_shipment" || status === "to_ship";
}

function canDirectPrint(order) {
  return !order?.printedAtUtc &&
    !isCancelledOrder(order) &&
    isPaidOrder(order) &&
    isAwaitingShipmentOrder(order);
}

function canReprint(order) {
  return Boolean(order?.printedAtUtc);
}

function getPrintBadge(order) {
  if (order.printError) {
    return { className: "failed", label: "打印失败" };
  }

  if (order.printedAtUtc) {
    return { className: "printed", label: `已打印 ${order.printCount || 1} 次` };
  }

  if (isCancelledOrder(order)) {
    return { className: "failed", label: "已取消" };
  }

  if (!isPaidOrder(order)) {
    return { className: "waiting", label: "未支付" };
  }

  if (!isAwaitingShipmentOrder(order)) {
    return { className: "waiting", label: `${translateStatus(order?.status)}，不在待发货` };
  }

  if (state.config?.autoPrintNewOrders && state.config?.autoPrintAfterBridgeCapture && !order.buyerAccountName) {
    return { className: "waiting", label: "等待用户名桥接" };
  }

  return { className: "pending", label: "待打印" };
}

function renderDeskMeta() {
  if (state.activeTab === "history") {
    if (!state.history.queried) {
      dom.deskMeta.textContent = "请先在左侧设定时间范围并查询历史订单。";
    } else {
      dom.deskMeta.textContent = `历史订单模式，第 ${state.history.pageIndex + 1} 页，本页 ${state.history.returnedCount} 单，共 ${state.history.totalCount} 单。`;
    }
    return;
  }

  dom.deskMeta.textContent = "缓存订单由后台轮询自动同步。支持右键操作、直接打印、实时重抓打印和桥接用户名自动回填。";
}

function renderSelectionBar() {
  const selectedCount = state.selectedIds.size;
  dom.selectionHint.textContent = `已选 ${selectedCount} 单`;
  dom.selectionBar.classList.toggle("hidden", selectedCount === 0);
}

function renderOrderCard(order) {
  const printBadge = getPrintBadge(order);
  const selected = state.selectedIds.has(order.orderId);
  const sourceText = order.source === "history" ? "历史检索" : "缓存同步";
  const sourceClass = order.source === "history" ? "source-history" : "source-cache";
  const handleText = order.buyerAccountName || "等待桥接 / 接口未返回";
  const buyerNameText = order.buyerName || "当前未返回";
  const userIdText = order.buyerPlatformUserId || "当前未返回";
  const emailText = emailAlias(order.buyerEmail) || "未返回";
  const addressText = order.recipientAddress || "未返回收件地址";
  const summaryText = order.primaryItemSummary || order.displayName || "接口未返回商品摘要";
  const sourceDetail = order.buyerAccountNameSource ? `来源：${order.buyerAccountNameSource}` : "来源：订单接口 / 未回填";
  const canPrint = canDirectPrint(order);
  const canReprintOrder = canReprint(order);
  const primaryAction = canReprintOrder ? "reprint" : "print";
  const actionLabel = canReprintOrder ? "重新打印" : "直接打印";

  return `
    <article class="order-card ${selected ? "is-selected" : ""}" data-order-id="${escapeHtml(order.orderId)}">
      <div class="order-card-header">
        <div class="order-card-main">
          <div class="order-card-meta">
            <label class="check-wrap">
              <input type="checkbox" data-select-order="${escapeHtml(order.orderId)}" ${selected ? "checked" : ""}>
              <span>选择</span>
            </label>
            <span class="source-badge ${sourceClass}">${escapeHtml(sourceText)}</span>
            <span class="print-badge ${printBadge.className}">${escapeHtml(printBadge.label)}</span>
            <span class="status-pill">${escapeHtml(translateStatus(order.status))}</span>
            ${order.buyerAccountNameSource ? '<span class="source-badge source-bridge">后台桥接</span>' : ""}
          </div>

          <button class="order-anchor" type="button" data-order-action="preview" data-order-id="${escapeHtml(order.orderId)}">
            订单 #${escapeHtml(order.orderId)}
          </button>
        </div>

        <div class="order-time-box">
          <span class="meta-kicker">支付时间</span>
          <strong class="meta-time">${escapeHtml(formatDateShort(order.paidAtUtc || order.createdAtUtc))}</strong>
          <span class="meta-sub">创建：${escapeHtml(formatDateShort(order.createdAtUtc))}</span>
        </div>
      </div>

      <div class="order-card-body">
        <section class="info-block">
          <span class="info-label">客户识别</span>
          <div class="info-primary">${escapeHtml(handleText)}</div>
          <div class="info-secondary">${escapeHtml(sourceDetail)}</div>
          <div class="info-minor">TikTok 名称：${escapeHtml(buyerNameText)}</div>
          <div class="info-minor">Buyer User ID：${escapeHtml(userIdText)}</div>
          <div class="info-minor">邮箱别名：${escapeHtml(emailText)}</div>
        </section>

        <section class="info-block">
          <span class="info-label">收件信息</span>
          <div class="info-primary">${escapeHtml(order.recipientName || "未返回")}</div>
          <div class="info-secondary">${escapeHtml(order.recipientPhone || "未返回电话")}</div>
          <div class="info-minor">${escapeHtml(addressText)}</div>
        </section>

        <section class="info-block">
          <span class="info-label">商品与金额</span>
          <div class="info-primary">${escapeHtml(summaryText)}</div>
          <div class="info-secondary">金额：${escapeHtml(formatMoney(order.totalAmount, order.currency))}</div>
          <div class="info-minor">商品款数：${escapeHtml(String(order.itemCount || 0))} · 总件数：${escapeHtml(formatQuantityValue(order.totalQuantity || 0))}</div>
        </section>

        <aside class="order-side-actions">
          <button class="small-button" type="button" data-order-action="preview" data-order-id="${escapeHtml(order.orderId)}">打开预览</button>
          ${(canPrint || canReprintOrder)
            ? `<button class="small-button primary" type="button" data-order-action="${escapeHtml(primaryAction)}" data-order-id="${escapeHtml(order.orderId)}">${escapeHtml(actionLabel)}</button>`
            : ""}
          <button class="small-button" type="button" data-order-action="fresh-print" data-order-id="${escapeHtml(order.orderId)}">实时拉详情并打印</button>
        </aside>
      </div>
    </article>
  `;
}

function renderOrders() {
  updateHeaderBadges();
  renderDeskMeta();
  renderSelectionBar();
  renderMetrics();

  dom.orderList.className = `order-list order-list--${state.orderView}`;

  dom.tabButtons.forEach((button) => {
    button.classList.toggle("active", button.dataset.tab === state.activeTab);
  });

  dom.viewButtons.forEach((button) => {
    button.classList.toggle("active", button.dataset.viewMode === state.orderView);
  });

  dom.historyPrevButton.disabled = !(state.activeTab === "history" && state.history.pageIndex > 0 && !state.history.loading);
  dom.historyNextButton.disabled = !(state.activeTab === "history" && Boolean(state.history.nextPageToken) && !state.history.loading);

  const orders = getVisibleOrders();
  if (!orders.length) {
    dom.orderList.innerHTML = `<div class="empty-state">${escapeHtml(
      state.activeTab === "history" && !state.history.queried
        ? "请先在左侧发起历史订单查询。"
        : "当前筛选条件下没有订单。"
    )}</div>`;
    return;
  }

  dom.orderList.innerHTML = orders.map(renderOrderCard).join("");
}

function buildStatusTone(type) {
  if (type === "error") {
    return "is-error";
  }
  if (type === "warn") {
    return "is-warn";
  }
  if (type === "ok") {
    return "is-ok";
  }
  return "";
}

function renderSummaryCard(label, value, tone = "") {
  return `
    <article class="preview-card ${buildStatusTone(tone)}">
      <strong>${escapeHtml(label)}</strong>
      <div class="preview-value">${escapeHtml(value || "—")}</div>
    </article>
  `;
}

function renderFieldRow(field, selectable = true) {
  const checked = selectable && state.selectedRawFieldPaths.has(field.path);
  return `
    <article class="field-row">
      <div class="field-row-top">
        <div class="field-meta">
          <strong class="field-label">${escapeHtml(field.label || field.path || "未命名字段")}</strong>
          <div class="field-path">${escapeHtml(field.path || "")}</div>
        </div>
        ${selectable
          ? `<label class="field-select"><input type="checkbox" data-select-field="${escapeHtml(field.path || "")}" ${checked ? "checked" : ""}><span>加入票面</span></label>`
          : ""}
      </div>
      <pre class="field-value">${escapeHtml(field.displayValue || field.valueJson || "")}</pre>
      <div class="preview-tools">
        <button class="secondary-button" type="button" data-copy-field="${escapeHtml(field.path || "")}">复制字段值</button>
      </div>
    </article>
  `;
}

function getFilteredRawFields() {
  const fields = state.preview.data?.rawFields || [];
  const mode = state.preview.fieldMode;
  const query = normalizeText(state.preview.fieldSearch);

  return fields.filter((field) => {
    const haystack = normalizeText(`${field.label || ""} ${field.path || ""}`);

    if (mode === "buyer" && !(haystack.includes("buyer") || haystack.includes("nickname") || haystack.includes("user_id"))) {
      return false;
    }

    if (mode === "name" && !(haystack.includes("name") || haystack.includes("nickname") || haystack.includes("display") || haystack.includes("名称") || haystack.includes("昵称"))) {
      return false;
    }

    return !query || haystack.includes(query);
  });
}

function buildSelectedFieldText(fields) {
  return fields
    .filter((field) => state.selectedRawFieldPaths.has(field.path))
    .map((field) => `${field.label || field.path}\n路径：${field.path}\n值：${field.displayValue || field.valueJson || ""}`)
    .join("\n\n");
}

function getGroupedPreviewItems(preview) {
  if (Array.isArray(preview.groupedItems) && preview.groupedItems.length) {
    return preview.groupedItems;
  }

  const groups = new Map();
  for (const item of preview.items || []) {
    const title = String(item?.title || item?.sku || "未命名商品").trim();
    const sku = String(item?.sku || "").trim();
    const key = `${title.toLowerCase()}::${sku.toLowerCase()}`;
    const quantity = Number(item?.quantity || 1) || 1;

    if (!groups.has(key)) {
      groups.set(key, {
        title,
        sku,
        quantity
      });
      continue;
    }

    groups.get(key).quantity += quantity;
  }

  return [...groups.values()];
}

function renderPreviewOverview(preview) {
    const buyerNicknameText = preview.buyerAccountName || "当前这笔订单的真实详情响应未包含 buyer_nickname";
    const buyerNameText = preview.buyerName || "当前未返回";
    const buyerEmailText = preview.buyerEmailAlias || emailAlias(preview.buyerEmail) || "当前未返回";
    const buyerSourceText = preview.buyerAccountNameSource || "订单接口";
    const groupedItems = getGroupedPreviewItems(preview);
    const itemLines = groupedItems.length
        ? groupedItems.map((item) => `${item.title || "未命名商品"} × ${formatQuantityValue(item.quantity ?? 1)}${item.sku ? ` · SKU ${item.sku}` : ""}`).join("\n")
        : "接口未返回商品明细。";
    const groupedSummary = preview.groupedItemSummary || (groupedItems[0]
        ? `${groupedItems[0].title || "未命名商品"} × ${formatQuantityValue(groupedItems[0].quantity ?? 1)}${groupedItems.length > 1 ? `，另 ${groupedItems.length - 1} 款` : ""}`
        : "接口未返回商品摘要");
    const groupedItemCount = preview.groupedItemCount ?? groupedItems.length;
    const totalQuantity = preview.totalQuantity ?? groupedItems.reduce((total, item) => total + (Number(item.quantity || 1) || 1), 0);
  
    return `
      <section class="preview-section">
        <div class="preview-grid">
          ${renderSummaryCard("买家昵称 ID", buyerNicknameText, preview.buyerAccountName ? "ok" : "warn")}
        ${renderSummaryCard("昵称来源", buyerSourceText, preview.buyerAccountNameSource ? "ok" : "")}
        ${renderSummaryCard("Buyer User ID", preview.buyerPlatformUserId || "当前未返回")}
        ${renderSummaryCard("TikTok 名称", buyerNameText)}
        ${renderSummaryCard("买家邮箱别名", buyerEmailText)}
        ${renderSummaryCard("收件人", preview.recipientName || "—")}
        ${renderSummaryCard("收件电话", preview.recipientPhone || "—")}
        ${renderSummaryCard("订单金额", formatMoney(preview.totalAmount, preview.currency))}
      </div>

        <div class="preview-grid-tight">
          ${renderSummaryCard("支付时间", formatDate(preview.paidAtUtc))}
          ${renderSummaryCard("下单时间", formatDate(preview.createdAtUtc))}
          ${renderSummaryCard("最近打印", formatDate(preview.printedAtUtc))}
          ${renderSummaryCard("打印次数", String(preview.printCount ?? 0))}
          ${renderSummaryCard("商品款数", String(groupedItemCount || 0))}
          ${renderSummaryCard("总件数", formatQuantityValue(totalQuantity))}
        </div>
  
        ${renderSummaryCard("收件地址", preview.recipientAddress || "当前未返回完整地址")}
        ${renderSummaryCard("买家备注", preview.buyerMessage || "当前未返回买家备注")}
        ${renderSummaryCard("商品摘要", groupedSummary)}
        ${renderSummaryCard("合并后商品明细", itemLines)}
      </section>
    `;
  }

function renderPreviewTicket(preview) {
  return `
    <section class="preview-section">
      <div class="preview-tools">
        <button class="secondary-button" type="button" data-preview-action="copy-ticket">复制票面文本</button>
      </div>
      <article class="ticket-box">
        <pre>${escapeHtml(preview.ticketContent || "")}</pre>
      </article>
    </section>
  `;
}

function renderPreviewFields(preview) {
  const filteredRawFields = getFilteredRawFields();
  const buyerFields = preview.buyerFields || [];

  return `
    <section class="preview-section">
      <div class="preview-header">
        <div>
          <h3>买家相关接口字段</h3>
          <p class="preview-subtitle">这里优先核对 buyer_nickname、user_id、buyer_email 以及其它 buyer_* 字段。</p>
        </div>
        <div class="preview-tools">
          <span class="status-pill">${escapeHtml(String(preview.buyerFieldCount ?? 0))} 项</span>
          <button class="secondary-button" type="button" data-preview-action="copy-buyer-fields">复制买家字段</button>
        </div>
      </div>
      <div class="field-list">
        ${buyerFields.length
          ? buyerFields.map((field) => renderFieldRow(field, false)).join("")
          : '<div class="empty-state">当前这笔订单的详情响应里没有 buyer_* / user_id 相关字段。</div>'}
      </div>
    </section>

    <section class="preview-section">
      <div class="preview-header">
        <div>
          <h3>订单详情全部字段</h3>
          <p class="preview-subtitle">可搜索、可筛选、可勾选加入票面，并支持单字段复制。</p>
        </div>
        <span class="status-pill">${escapeHtml(String(preview.rawFieldCount ?? 0))} 项</span>
      </div>

      <div class="preview-filter-bar">
        <input class="preview-search" id="previewFieldSearch" type="text" value="${escapeHtml(state.preview.fieldSearch)}" placeholder="搜索字段：buyer、payment、address、name、logistics">
        <button class="chip-button ${state.preview.fieldMode === "all" ? "active" : ""}" type="button" data-field-mode="all">全部字段</button>
        <button class="chip-button ${state.preview.fieldMode === "buyer" ? "active" : ""}" type="button" data-field-mode="buyer">仅买家</button>
        <button class="chip-button ${state.preview.fieldMode === "name" ? "active" : ""}" type="button" data-field-mode="name">仅名称类</button>
      </div>

      <div class="preview-field-actions">
        <button class="secondary-button" type="button" data-field-action="select-visible">勾选当前可见字段</button>
        <button class="secondary-button" type="button" data-field-action="clear-all">清空勾选</button>
        <button class="secondary-button" type="button" data-field-action="buyer-only">只勾选买家字段</button>
        <button class="secondary-button" type="button" data-field-action="copy-selected">复制已勾选字段</button>
        <button class="primary-button" type="button" data-field-action="save">保存到票面配置</button>
      </div>

      <div class="field-list">
        ${filteredRawFields.length
          ? filteredRawFields.map((field) => renderFieldRow(field, true)).join("")
          : '<div class="empty-state">当前筛选条件下没有匹配字段。</div>'}
      </div>
    </section>
  `;
}

function renderPreviewJson(preview) {
  return `
    <section class="preview-section">
      <div class="preview-header">
        <div>
          <h3>原始返回</h3>
          <p class="preview-subtitle">如果怀疑字段映射不对，直接看这里的真实 JSON。这里展示的是接口实际返回内容。</p>
        </div>
        <div class="preview-tools">
          <button class="secondary-button" type="button" data-preview-action="copy-raw-order">复制订单 JSON</button>
          <button class="secondary-button" type="button" data-preview-action="copy-raw-response">复制整包 JSON</button>
        </div>
      </div>

      <details class="raw-box" open>
        <summary>原始订单 JSON</summary>
        <pre>${escapeHtml(preview.rawOrderJson || "")}</pre>
      </details>

      <details class="raw-box">
        <summary>原始整包 API JSON</summary>
        <pre>${escapeHtml(preview.rawResponseJson || "")}</pre>
      </details>

      <details class="raw-box">
        <summary>当前已勾选字段文本</summary>
        <pre>${escapeHtml(buildSelectedFieldText(preview.rawFields || []))}</pre>
      </details>
    </section>
  `;
}

function renderPreview() {
  if (state.preview.loading) {
    dom.previewPlaceholder.classList.add("hidden");
    dom.previewContent.classList.remove("hidden");
    dom.previewContent.innerHTML = '<div class="preview-card skeleton">正在加载订单预览…</div>';
    return;
  }

  if (!state.preview.data) {
    dom.previewPlaceholder.classList.remove("hidden");
    dom.previewContent.classList.add("hidden");
    dom.previewContent.innerHTML = "";
    return;
  }

  const preview = state.preview.data;
  const printBadge = preview.printedAtUtc ? `已打印 ${preview.printCount || 1} 次` : "尚未打印";
  const canPrint = canDirectPrint(preview);
  const canReprintOrder = canReprint(preview);
  const primaryAction = canReprintOrder ? "reprint" : "print";
  const primaryActionLabel = canReprintOrder ? "重新打印" : (canPrint ? "直接打印" : "当前不可打印");
  const tabButtons = Object.entries(previewTabs).map(([key, label]) => `
    <button class="chip-button ${state.preview.tab === key ? "active" : ""}" type="button" data-preview-tab="${escapeHtml(key)}">${escapeHtml(label)}</button>
  `).join("");

  let tabContent = renderPreviewOverview(preview);
  if (state.preview.tab === "ticket") {
    tabContent = renderPreviewTicket(preview);
  } else if (state.preview.tab === "fields") {
    tabContent = renderPreviewFields(preview);
  } else if (state.preview.tab === "json") {
    tabContent = renderPreviewJson(preview);
  }

  dom.previewPlaceholder.classList.add("hidden");
  dom.previewContent.classList.remove("hidden");
  dom.previewContent.innerHTML = `
    <section class="preview-section">
      <div class="preview-header">
        <div>
          <p class="eyebrow">Order Preview</p>
          <h2>订单 #${escapeHtml(preview.orderId || "")}</h2>
          <p class="preview-subtitle">${escapeHtml(translateStatus(preview.status))} · ${escapeHtml(printBadge)}</p>
        </div>
        <div class="preview-actions">
          <button class="primary-button" type="button" data-preview-action="${escapeHtml(primaryAction)}" ${!canReprintOrder && !canPrint ? "disabled" : ""}>${escapeHtml(primaryActionLabel)}</button>
          <button class="secondary-button" type="button" data-preview-action="fresh-print">实时拉详情并打印</button>
          <button class="secondary-button" type="button" data-preview-action="copy-order">复制订单号</button>
          <button class="secondary-button" type="button" data-preview-action="copy-buyer-nickname">复制买家昵称 ID</button>
          <button class="secondary-button" type="button" data-preview-action="copy-buyer-user-id">复制买家 User ID</button>
        </div>
      </div>

      <div class="preview-tab-strip">
        ${tabButtons}
      </div>
    </section>

    ${tabContent}
  `;
}

function attachPreviewEvents() {
  const preview = state.preview.data;
  if (!preview) {
    return;
  }

  dom.previewContent.querySelectorAll("[data-preview-tab]").forEach((button) => {
    button.addEventListener("click", () => {
      state.preview.tab = button.dataset.previewTab;
      renderPreview();
      attachPreviewEvents();
    });
  });

    dom.previewContent.querySelectorAll("[data-preview-action]").forEach((button) => {
      button.addEventListener("click", async () => {
        const action = button.dataset.previewAction;

        if (action === "print") {
          await printOrder(preview.orderId, false);
        }
        if (action === "reprint") {
          await reprintOrder(preview.orderId);
        }
        if (action === "fresh-print") {
          await printOrder(preview.orderId, true);
        }
      if (action === "copy-order") {
        await copyText(preview.orderId, "订单号已复制。");
      }
      if (action === "copy-buyer-nickname") {
        await copyText(preview.buyerAccountName || "", "买家昵称 ID 已复制。");
      }
      if (action === "copy-buyer-user-id") {
        await copyText(preview.buyerPlatformUserId || "", "买家 User ID 已复制。");
      }
      if (action === "copy-buyer-fields") {
        const text = (preview.buyerFields || [])
          .map((field) => `${field.label} (${field.path})\n${field.displayValue || field.valueJson || ""}`)
          .join("\n\n");
        await copyText(text, "买家字段已复制。");
      }
      if (action === "copy-raw-order") {
        await copyText(preview.rawOrderJson || "", "订单 JSON 已复制。");
      }
      if (action === "copy-raw-response") {
        await copyText(preview.rawResponseJson || "", "整包 API JSON 已复制。");
      }
      if (action === "copy-ticket") {
        await copyText(preview.ticketContent || "", "票面文本已复制。");
      }
    });
  });

  dom.previewContent.querySelector("#previewFieldSearch")?.addEventListener("input", (event) => {
    state.preview.fieldSearch = event.target.value;
    renderPreview();
    attachPreviewEvents();
  });

  dom.previewContent.querySelectorAll("[data-field-mode]").forEach((button) => {
    button.addEventListener("click", () => {
      state.preview.fieldMode = button.dataset.fieldMode;
      renderPreview();
      attachPreviewEvents();
    });
  });

  dom.previewContent.querySelectorAll("[data-copy-field]").forEach((button) => {
    button.addEventListener("click", async () => {
      const field = [...(preview.rawFields || []), ...(preview.buyerFields || [])]
        .find((item) => item.path === button.dataset.copyField);

      await copyText(field?.displayValue || field?.valueJson || "", `字段 ${field?.label || field?.path || ""} 已复制。`);
    });
  });

  dom.previewContent.querySelectorAll("[data-select-field]").forEach((checkbox) => {
    checkbox.addEventListener("change", () => {
      const path = checkbox.dataset.selectField;
      if (checkbox.checked) {
        state.selectedRawFieldPaths.add(path);
      } else {
        state.selectedRawFieldPaths.delete(path);
      }
      updateSelectedRawFieldCount();
      markConfigDirty(true);
    });
  });

  dom.previewContent.querySelector("[data-field-action=\"select-visible\"]")?.addEventListener("click", () => {
    getFilteredRawFields().forEach((field) => state.selectedRawFieldPaths.add(field.path));
    updateSelectedRawFieldCount();
    markConfigDirty(true);
    renderPreview();
    attachPreviewEvents();
  });

  dom.previewContent.querySelector("[data-field-action=\"clear-all\"]")?.addEventListener("click", () => {
    state.selectedRawFieldPaths.clear();
    updateSelectedRawFieldCount();
    markConfigDirty(true);
    renderPreview();
    attachPreviewEvents();
  });

  dom.previewContent.querySelector("[data-field-action=\"buyer-only\"]")?.addEventListener("click", () => {
    state.selectedRawFieldPaths.clear();
    (preview.buyerFields || []).forEach((field) => state.selectedRawFieldPaths.add(field.path));
    updateSelectedRawFieldCount();
    markConfigDirty(true);
    renderPreview();
    attachPreviewEvents();
  });

  dom.previewContent.querySelector("[data-field-action=\"copy-selected\"]")?.addEventListener("click", async () => {
    await copyText(buildSelectedFieldText(preview.rawFields || []), "已勾选字段已复制。");
  });

  dom.previewContent.querySelector("[data-field-action=\"save\"]")?.addEventListener("click", async () => {
    try {
      await persistRawFieldSelection(true);
    } catch (error) {
      showToast(error.message || "保存打印字段失败。", "error");
    }
  });
}

function showContextMenu(event, order) {
  event.preventDefault();
  state.contextOrder = order;

  const menuWidth = 220;
  const menuHeight = 220;
  const left = Math.min(event.clientX, window.innerWidth - menuWidth - 16);
  const top = Math.min(event.clientY, window.innerHeight - menuHeight - 16);

  dom.contextMenu.style.left = `${left}px`;
  dom.contextMenu.style.top = `${top}px`;
  dom.contextMenu.classList.remove("hidden");
}

function hideContextMenu() {
  dom.contextMenu.classList.add("hidden");
  state.contextOrder = null;
}

function handleOrderListContextMenu(event) {
  const target = event.target instanceof HTMLElement ? event.target.closest("[data-order-id]") : null;
  if (!(target instanceof HTMLElement)) {
    return;
  }

  const order = getOrderById(target.dataset.orderId);
  if (!order) {
    return;
  }

  showContextMenu(event, order);
}

async function openPreview(orderId, showLoading = true) {
  state.preview.orderId = orderId;

  if (showLoading) {
    state.preview.loading = true;
    renderPreview();
  }

  try {
    const preview = await requestJson(`/api/orders/${encodeURIComponent(orderId)}/preview`);
    state.preview.data = preview;
    state.preview.loading = false;
    renderPreview();
    attachPreviewEvents();
  } catch (error) {
    state.preview.loading = false;
    state.preview.data = null;
    renderPreview();
    showToast(error.message || "加载预览失败。", "error");
  }
}

async function handleOrderListClick(event) {
  const target = event.target;
  if (!(target instanceof HTMLElement)) {
    return;
  }

  const checkbox = target.closest("[data-select-order]");
  if (checkbox instanceof HTMLInputElement) {
    const orderId = checkbox.dataset.selectOrder;
    if (checkbox.checked) {
      state.selectedIds.add(orderId);
    } else {
      state.selectedIds.delete(orderId);
    }
    renderSelectionBar();
    renderMetrics();
    return;
  }

  const actionButton = target.closest("[data-order-action]");
  if (!(actionButton instanceof HTMLElement)) {
    return;
  }

  const orderId = actionButton.dataset.orderId;
  const action = actionButton.dataset.orderAction;
  if (!orderId) {
    return;
  }

  if (action === "preview") {
    await openPreview(orderId);
  }
  if (action === "print") {
    await printOrder(orderId, false);
  }
  if (action === "fresh-print") {
    await printOrder(orderId, true);
  }
}

async function loadConfig() {
  const config = await requestJson("/api/config");
  fillConfig(config);
}

async function loadPrinters() {
  const printers = await requestJson("/api/printers");
  renderPrinters(printers);
}

async function loadStatusAndCachedOrders() {
  const [status, cachedOrders] = await Promise.all([
    requestJson("/api/status"),
    requestJson("/api/orders/recent")
  ]);

  state.status = status;
  state.cachedOrders = Array.isArray(cachedOrders) ? cachedOrders : [];
  renderStatus();
  renderOrders();
}

function setSaveButtonState(isSaving) {
  if (!dom.saveConfigButton) {
    return;
  }

  dom.saveConfigButton.disabled = isSaving;
  dom.saveConfigButton.textContent = isSaving ? "保存中…" : "保存配置";
}

function getConfigAutoSaveDelay(element) {
  if (element instanceof HTMLSelectElement) {
    return 120;
  }

  if (element instanceof HTMLInputElement && element.type === "checkbox") {
    return 120;
  }

  return 700;
}

function scheduleConfigAutoSave(element) {
  if (state.suppressDirtyTracking) {
    return;
  }

  if (state.configAutoSaveTimer) {
    window.clearTimeout(state.configAutoSaveTimer);
  }

  state.configAutoSaveTimer = window.setTimeout(() => {
    state.configAutoSaveTimer = 0;
    saveConfig({ silent: true }).catch(() => {});
  }, getConfigAutoSaveDelay(element));
}

async function saveConfig(eventOrOptions, maybeOptions) {
  const isEvent = eventOrOptions && typeof eventOrOptions.preventDefault === "function";
  const event = isEvent ? eventOrOptions : null;
  const options = isEvent ? (maybeOptions || {}) : (eventOrOptions || {});

  event?.preventDefault();

  if (state.configAutoSaveTimer) {
    window.clearTimeout(state.configAutoSaveTimer);
    state.configAutoSaveTimer = 0;
  }

  if (state.configSaveInFlight) {
    state.configSaveQueued = true;
    return;
  }

  const payload = getConfigPayload();
  const payloadSignature = buildConfigSignature(payload);
  if (!options.force && payloadSignature === state.savedConfigSignature) {
    state.dirtyConfig = false;
    state.configSaveError = "";
    renderSignals();
    return;
  }

  state.configSaveInFlight = true;
  state.configSaveError = "";
  renderSignals();
  setSaveButtonState(true);

  try {
    await requestJson("/api/config", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    state.savedConfigSignature = payloadSignature;
    state.dirtyConfig = false;
    state.configSaveError = "";
    renderSignals();
    await loadConfig();
    renderOrders();
    if (!options.silent) {
      showToast("配置已自动保存。", "success");
    }
  } catch (error) {
    state.configSaveError = error.message || "保存配置失败。";
    renderSignals();
    showToast(error.message || "保存配置失败。", "error");
    setMessage(error.message || "保存配置失败。", "error");
  } finally {
    state.configSaveInFlight = false;
    setSaveButtonState(false);
    renderSignals();

    if (state.configSaveQueued || buildConfigSignature(getConfigPayload()) !== state.savedConfigSignature) {
      state.configSaveQueued = false;
      saveConfig({ silent: true }).catch(() => {});
    }
  }
}

async function persistRawFieldSelection(showSuccessToast = false) {
  const payload = getConfigPayload();
  await requestJson("/api/config", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload)
  });

  state.savedConfigSignature = buildConfigSignature(payload);
  state.dirtyConfig = false;
  await loadConfig();
  updateSelectedRawFieldCount();
  renderSignals();
  if (showSuccessToast) {
      showToast("票面字段已自动保存。", "success");
  }
}

function applyTokenResult(result) {
  dom.accessToken.value = result.access_token || result.accessToken || dom.accessToken.value;
  dom.refreshTokenInput.value = result.refresh_token || result.refreshToken || dom.refreshTokenInput.value;
  dom.shopId.value = result.shop_cipher || result.shopCipher || dom.shopId.value;
  markConfigDirty(true);
}

async function refreshToken() {
  dom.refreshTokenButton.disabled = true;

  try {
    const result = await requestJson("/api/token/refresh", { method: "POST" });
    applyTokenResult(result);
    await saveConfig();
    showToast("Access Token 已刷新。", "success");
  } catch (error) {
    showToast(error.message || "刷新 Token 失败。", "error");
    setMessage(error.message || "刷新 Token 失败。", "error");
  } finally {
    dom.refreshTokenButton.disabled = false;
  }
}

async function exchangeToken() {
  dom.exchangeTokenButton.disabled = true;

  try {
    const result = await requestJson("/api/token/exchange", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ authCodeOrUrl: dom.authCodeOrUrl.value.trim() })
    });

    applyTokenResult(result);
    await saveConfig();
    showToast("授权码换 Token 成功。", "success");
  } catch (error) {
    showToast(error.message || "换取 Token 失败。", "error");
    setMessage(error.message || "换取 Token 失败。", "error");
  } finally {
    dom.exchangeTokenButton.disabled = false;
  }
}

function setHistoryRange(days) {
  const to = new Date();
  const from = new Date(to.getTime() - days * 24 * 60 * 60 * 1000);
  dom.historyTo.value = toLocalInputValue(to);
  dom.historyFrom.value = toLocalInputValue(from);
  dom.rangeButtons.forEach((button) => {
    button.classList.toggle("active", Number.parseInt(button.dataset.rangeDays, 10) === days);
  });
}

async function runHistoryQuery({ reset }) {
  state.history.loading = true;
  dom.historySearchButton.disabled = true;

  try {
    if (reset) {
      state.history.pageIndex = 0;
      state.history.pageTokens = [""];
    }

    const payload = {
      fromUtc: fromLocalInputValue(dom.historyFrom.value),
      toUtc: fromLocalInputValue(dom.historyTo.value),
      pageSize: Number.parseInt(dom.historyPageSize.value, 10),
      pageToken: state.history.pageTokens[state.history.pageIndex] || "",
      keyword: dom.historyKeyword.value.trim(),
      statuses: dom.historyStatus.value ? [dom.historyStatus.value] : []
    };

    const result = await requestJson("/api/orders/history", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload)
    });

    state.history.queried = true;
    state.history.query = payload;
    state.history.orders = result.orders || [];
    state.history.totalCount = result.totalCount || 0;
    state.history.returnedCount = result.returnedCount || 0;
    state.history.nextPageToken = result.nextPageToken || "";
    renderHistorySummary();
    renderOrders();
  } catch (error) {
    showToast(error.message || "查询历史订单失败。", "error");
    setMessage(error.message || "查询历史订单失败。", "error");
  } finally {
    state.history.loading = false;
    dom.historySearchButton.disabled = false;
    renderOrders();
  }
}

async function goToHistoryNextPage() {
  if (!state.history.nextPageToken) {
    return;
  }

  state.history.pageTokens.push(state.history.nextPageToken);
  state.history.pageIndex += 1;
  await runHistoryQuery({ reset: false });
}

async function goToHistoryPreviousPage() {
  if (state.history.pageIndex <= 0) {
    return;
  }

  state.history.pageIndex -= 1;
  await runHistoryQuery({ reset: false });
}

function setActiveTab(tab) {
  state.activeTab = tab;
  renderOrders();
}

function setOrderView(viewMode) {
  state.orderView = viewMode;
  renderOrders();
}

async function pollOrders() {
  dom.pollButton.disabled = true;

  try {
    const result = await requestJson("/api/poll", { method: "POST" });
    await loadStatusAndCachedOrders();
    if (state.activeTab === "history" && state.history.queried) {
      await runHistoryQuery({ reset: false });
    }
    if (state.preview.orderId) {
      await openPreview(state.preview.orderId, false);
    }
    showToast(`同步完成：发现 ${result.ordersFound ?? 0} 单，处理 ${result.ordersProcessed ?? 0} 单。`, "success");
  } catch (error) {
    showToast(error.message || "同步订单失败。", "error");
    setMessage(error.message || "同步订单失败。", "error");
  } finally {
    dom.pollButton.disabled = false;
  }
}

async function refreshWorkspace() {
  dom.refreshWorkspaceButton.disabled = true;

  try {
    await Promise.all([loadConfig(), loadPrinters(), loadStatusAndCachedOrders()]);
    if (state.activeTab === "history" && state.history.queried) {
      await runHistoryQuery({ reset: false });
    }
    if (state.preview.orderId) {
      await openPreview(state.preview.orderId, false);
    }
    showToast("工作台已刷新。", "success");
  } catch (error) {
    showToast(error.message || "刷新工作台失败。", "error");
    setMessage(error.message || "刷新工作台失败。", "error");
  } finally {
    dom.refreshWorkspaceButton.disabled = false;
  }
}

async function refreshActiveDataset() {
  if (state.activeTab === "history" && state.history.queried) {
    await runHistoryQuery({ reset: false });
    return;
  }

  await loadStatusAndCachedOrders();
  showToast("当前列表已刷新。", "success");
}

async function printOrder(orderId, fresh) {
  const label = fresh ? "实时拉取并打印" : "打印";

  try {
    await requestJson(`/api/orders/${encodeURIComponent(orderId)}/print${fresh ? "?fresh=true" : ""}`, {
      method: "POST"
    });

    await loadStatusAndCachedOrders();
    if (state.activeTab === "history" && state.history.queried) {
      await runHistoryQuery({ reset: false });
    }
    if (state.preview.orderId === orderId) {
      await openPreview(orderId, false);
    }
    showToast(`${label}成功：${orderId}`, "success");
  } catch (error) {
    showToast(error.message || `${label}失败。`, "error");
    setMessage(error.message || `${label}失败。`, "error");
  }
}

async function reprintOrder(orderId) {
  try {
    await requestJson(`/api/orders/${encodeURIComponent(orderId)}/reprint`, {
      method: "POST"
    });

    await loadStatusAndCachedOrders();
    if (state.activeTab === "history" && state.history.queried) {
      await runHistoryQuery({ reset: false });
    }
    if (state.preview.orderId === orderId) {
      await openPreview(orderId, false);
    }
    showToast(`重新打印成功：${orderId}`, "success");
  } catch (error) {
    showToast(error.message || "重新打印失败。", "error");
    setMessage(error.message || "重新打印失败。", "error");
  }
}

async function printSelectedOrders() {
  const orderIds = [...state.selectedIds];
  if (!orderIds.length) {
    showToast("请先选择要打印的订单。", "error");
    return;
  }

  dom.printSelectedButton.disabled = true;
  dom.selectionPrintButton.disabled = true;

  try {
    const result = await requestJson("/api/orders/print-selected", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ orderIds })
    });

    (result.successIds || []).forEach((orderId) => state.selectedIds.delete(orderId));
    await loadStatusAndCachedOrders();
    if (state.activeTab === "history" && state.history.queried) {
      await runHistoryQuery({ reset: false });
    }
    if (state.preview.orderId) {
      await openPreview(state.preview.orderId, false);
    }
    showToast(`批量打印完成：成功 ${result.printed ?? 0} 单。`, "success");
  } catch (error) {
    showToast(error.message || "批量打印失败。", "error");
    setMessage(error.message || "批量打印失败。", "error");
  } finally {
    dom.printSelectedButton.disabled = false;
    dom.selectionPrintButton.disabled = false;
    renderSelectionBar();
    renderMetrics();
  }
}

async function disablePwaShell() {
  if (!("serviceWorker" in navigator)) {
    return;
  }
  try {
    const registrations = await navigator.serviceWorker.getRegistrations();
    await Promise.all(registrations.map((registration) => registration.unregister()));
  } catch {
    // Ignore local cleanup failures.
  }
  if (!("caches" in window)) {
    return;
  }
  try {
    const cacheNames = await caches.keys();
    await Promise.all(cacheNames.map((cacheName) => caches.delete(cacheName)));
  } catch {
    // Ignore cache cleanup failures.
  }
}
function scrollToSection(sectionId, button) {
  document.getElementById(sectionId)?.scrollIntoView({ behavior: "smooth", block: "start" });
  dom.navButtons.forEach((item) => item.classList.remove("active"));
  button.classList.add("active");
}

function handleConfigFieldChange(event) {
  toggleCustomPaperFields();
  refreshDirtyState();
  updateHeaderBadges();
  renderOrders();
  scheduleConfigAutoSave(event?.currentTarget || null);
}

function getTrackedConfigElements() {
  return [
    dom.storeName,
    dom.appKey,
    dom.appSecret,
    dom.accessToken,
    dom.refreshTokenInput,
    dom.shopId,
    dom.printerName,
    dom.paperSize,
    dom.customPaperWidthMm,
    dom.customPaperHeightMm,
    dom.marginMm,
    dom.paperWidthCharacters,
    dom.baseFontSize,
    dom.minFontSize,
    dom.autoPrintNewOrders,
    dom.autoPrintAfterBridgeCapture,
    dom.showBuyerAccountName,
    dom.showBuyerPlatformUserId,
    dom.showBuyerName,
    dom.showBuyerEmail,
    dom.showRecipientPhone,
    dom.showBuyerMessage,
    dom.showOrderAmounts,
    dom.showItemDetails,
    dom.showSku,
    dom.showPaidTime,
    dom.showCreatedTime
  ].filter(Boolean);
}

function attachConfigDirtyTracking() {
  getTrackedConfigElements().forEach((element) => {
    element.addEventListener("input", handleConfigFieldChange);
    element.addEventListener("change", handleConfigFieldChange);
  });
}

async function initialize() {
  setHistoryRange(30);
  toggleCustomPaperFields();
  attachConfigDirtyTracking();
  await Promise.all([loadConfig(), loadPrinters(), loadStatusAndCachedOrders(), disablePwaShell()]);
  renderHistorySummary();
}

dom.configForm.addEventListener("submit", saveConfig);

dom.pollButton.addEventListener("click", pollOrders);
dom.refreshTokenButton.addEventListener("click", refreshToken);
dom.refreshWorkspaceButton.addEventListener("click", refreshWorkspace);
dom.printSelectedButton.addEventListener("click", printSelectedOrders);
dom.selectionPrintButton.addEventListener("click", printSelectedOrders);
dom.exchangeTokenButton.addEventListener("click", exchangeToken);
dom.historySearchButton.addEventListener("click", async () => runHistoryQuery({ reset: true }));
dom.historyPrevButton.addEventListener("click", goToHistoryPreviousPage);
dom.historyNextButton.addEventListener("click", goToHistoryNextPage);
dom.refreshActiveDatasetButton.addEventListener("click", refreshActiveDataset);
dom.selectVisibleButton.addEventListener("click", () => {
  getVisibleOrders().forEach((order) => state.selectedIds.add(order.orderId));
  renderSelectionBar();
  renderMetrics();
  renderOrders();
});
dom.clearSelectedButton.addEventListener("click", () => {
  state.selectedIds.clear();
  renderSelectionBar();
  renderMetrics();
  renderOrders();
});
dom.selectionClearButton.addEventListener("click", () => {
  state.selectedIds.clear();
  renderSelectionBar();
  renderMetrics();
  renderOrders();
});

dom.listSearch.addEventListener("input", renderOrders);
dom.listPrintState.addEventListener("change", renderOrders);

dom.tabButtons.forEach((button) => {
  button.addEventListener("click", () => setActiveTab(button.dataset.tab));
});

dom.viewButtons.forEach((button) => {
  button.addEventListener("click", () => setOrderView(button.dataset.viewMode));
});

dom.rangeButtons.forEach((button) => {
  button.addEventListener("click", () => setHistoryRange(Number.parseInt(button.dataset.rangeDays, 10)));
});

dom.navButtons.forEach((button, index) => {
  if (index === 0) {
    button.classList.add("active");
  }
  button.addEventListener("click", () => scrollToSection(button.dataset.scrollTarget, button));
});

dom.printerChips.addEventListener("click", (event) => {
  const button = event.target instanceof HTMLElement ? event.target.closest("[data-printer-chip]") : null;
  if (!(button instanceof HTMLElement)) {
    return;
  }

  dom.printerName.value = button.dataset.printerChip || "";
  markConfigDirty(true);
  updateHeaderBadges();
});

dom.orderList.addEventListener("click", (event) => {
  handleOrderListClick(event).catch(() => {});
});
dom.orderList.addEventListener("contextmenu", handleOrderListContextMenu);

dom.contextButtons.forEach((button) => {
  button.addEventListener("click", async () => {
    const order = state.contextOrder;
    hideContextMenu();
    if (!order) {
      return;
    }

    if (button.dataset.menuAction === "preview") {
      await openPreview(order.orderId);
    }
    if (button.dataset.menuAction === "print") {
      await printOrder(order.orderId, false);
    }
    if (button.dataset.menuAction === "fresh-print") {
      await printOrder(order.orderId, true);
    }
    if (button.dataset.menuAction === "copy-order") {
      await copyText(order.orderId, "订单号已复制。");
    }
    if (button.dataset.menuAction === "copy-buyer-nickname") {
      await copyText(order.buyerAccountName || "", "买家昵称 ID 已复制。");
    }
    if (button.dataset.menuAction === "copy-buyer-user-id") {
      await copyText(order.buyerPlatformUserId || "", "买家 User ID 已复制。");
    }
  });
});

window.addEventListener("click", (event) => {
  if (event.target instanceof HTMLElement && event.target.closest(".context-menu")) {
    return;
  }
  hideContextMenu();
});

window.addEventListener("resize", hideContextMenu);
window.addEventListener("scroll", hideContextMenu, true);
window.addEventListener("keydown", (event) => {
  if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "s") {
    event.preventDefault();
    saveConfig();
  }

  if (event.key === "Escape") {
    hideContextMenu();
  }

  if (
    event.key === "/" &&
    !(event.target instanceof HTMLInputElement) &&
    !(event.target instanceof HTMLTextAreaElement) &&
    !(event.target instanceof HTMLSelectElement)
  ) {
    event.preventDefault();
    dom.listSearch.focus();
  }
});

window.setInterval(() => {
  loadStatusAndCachedOrders().catch(() => {});
}, 15000);

initialize().catch((error) => {
  showToast(error.message || "初始化失败。", "error");
  setMessage(error.message || "初始化失败。", "error");
});

