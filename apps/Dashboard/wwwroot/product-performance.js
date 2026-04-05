const state = {
  config: null,
  summary: null,
  activePreset: "month"
};

const elements = {
  statusChip: document.getElementById("statusChip"),
  timezoneChip: document.getElementById("timezoneChip"),
  generatedAtChip: document.getElementById("generatedAtChip"),
  storeSelect: document.getElementById("storeSelect"),
  monthSelect: document.getElementById("monthSelect"),
  fromDateInput: document.getElementById("fromDateInput"),
  toDateInput: document.getElementById("toDateInput"),
  filtersForm: document.getElementById("filtersForm"),
  presetButtons: Array.from(document.querySelectorAll(".preset-button")),
  productIdsInput: document.getElementById("productIdsInput"),
  applyProductIdsButton: document.getElementById("applyProductIdsButton"),
  clearProductIdsButton: document.getElementById("clearProductIdsButton"),
  includeTikTokDiscountToggle: document.getElementById("includeTikTokDiscountToggle"),
  includeBuyerShippingFeeToggle: document.getElementById("includeBuyerShippingFeeToggle"),
  deductPlatformFeeToggle: document.getElementById("deductPlatformFeeToggle"),
  deductLogisticsCostToggle: document.getElementById("deductLogisticsCostToggle"),
  platformFeeRateInput: document.getElementById("platformFeeRateInput"),
  logisticsCostInput: document.getElementById("logisticsCostInput"),
  exportXlsxButton: document.getElementById("exportXlsxButton"),
  overviewCards: document.getElementById("overviewCards"),
  trackedProductChips: document.getElementById("trackedProductChips"),
  activeProductHint: document.getElementById("activeProductHint"),
  activeProductChips: document.getElementById("activeProductChips"),
  productPerformanceGrid: document.getElementById("productPerformanceGrid")
};

document.addEventListener("DOMContentLoaded", async () => {
  wireEvents();
  renderMonthOptions();
  hydrateFromUrl();
  if (!elements.fromDateInput.value || !elements.toDateInput.value) {
    setDatePreset("month");
  }

  try {
    await loadConfig();
    await loadSummary();
  } catch (error) {
    console.error(error);
    setStatus(error?.message || "初始化失败", true);
  }
});

function wireEvents() {
  elements.filtersForm.addEventListener("submit", async (event) => {
    event.preventDefault();
    await loadSummary();
  });

  elements.storeSelect.addEventListener("change", () => loadSummary());

  elements.monthSelect.addEventListener("change", async () => {
    const value = elements.monthSelect.value;
    if (!value) {
      return;
    }

    if (value === "custom") {
      state.activePreset = "custom";
      updatePresetButtons();
      return;
    }

    setMonthRange(value);
    await loadSummary();
  });

  [elements.fromDateInput, elements.toDateInput].forEach((input) => {
    input.addEventListener("change", () => {
      state.activePreset = "custom";
      elements.monthSelect.value = "custom";
      updatePresetButtons();
    });
  });

  elements.presetButtons.forEach((button) => {
    button.addEventListener("click", async () => {
      setDatePreset(button.dataset.preset || "month");
      await loadSummary();
    });
  });

  elements.applyProductIdsButton.addEventListener("click", async () => {
    await loadSummary();
  });

  elements.clearProductIdsButton.addEventListener("click", async () => {
    elements.productIdsInput.value = "";
    renderActiveProductChips();
    await loadSummary();
  });

  elements.productIdsInput.addEventListener("keydown", async (event) => {
    if (event.key !== "Enter") {
      return;
    }

    event.preventDefault();
    await loadSummary();
  });

  elements.productIdsInput.addEventListener("input", () => {
    renderActiveProductChips();
  });

  [
    elements.includeTikTokDiscountToggle,
    elements.includeBuyerShippingFeeToggle,
    elements.deductPlatformFeeToggle,
    elements.deductLogisticsCostToggle
  ].forEach((toggle) => toggle.addEventListener("change", () => loadSummary()));

  [elements.platformFeeRateInput, elements.logisticsCostInput].forEach((input) => {
    input.addEventListener("change", () => loadSummary());
  });

  elements.exportXlsxButton.addEventListener("click", () => {
    window.location.href = `/api/product-performance/export.xlsx?${buildQuery().toString()}`;
  });
}

async function loadConfig() {
  setStatus("正在读取产品配置...", false);
  const response = await fetch("/api/product-performance/config");
  if (!response.ok) {
    throw new Error("读取产品配置失败");
  }

  state.config = await response.json();
  elements.timezoneChip.textContent = `时区 ${state.config.timezone || "-"}`;
  renderStoreOptions();
  renderTrackedProductChips();
  renderActiveProductChips();
}

async function loadSummary() {
  setStatus("正在从 TikTok API 计算产品业绩...", false);
  const response = await fetch(`/api/product-performance/summary?${buildQuery().toString()}`);
  if (!response.ok) {
    throw new Error("读取产品业绩失败");
  }

  state.summary = await response.json();
  renderSummary();
  renderActiveProductChips();
  setStatus("产品业绩已更新", false);
}

function buildQuery() {
  const query = new URLSearchParams();
  query.set("store", elements.storeSelect.value || "all");
  if (elements.fromDateInput.value) {
    query.set("fromDate", elements.fromDateInput.value);
  }
  if (elements.toDateInput.value) {
    query.set("toDate", elements.toDateInput.value);
  }
  query.set("includeTikTokDiscount", elements.includeTikTokDiscountToggle.checked ? "true" : "false");
  query.set("includeBuyerShippingFee", elements.includeBuyerShippingFeeToggle.checked ? "true" : "false");
  query.set("deductPlatformFee", elements.deductPlatformFeeToggle.checked ? "true" : "false");
  query.set("deductLogisticsCost", elements.deductLogisticsCostToggle.checked ? "true" : "false");
  query.set("platformFeeRate", elements.platformFeeRateInput.value || "0");
  query.set("logisticsCostPerOrder", elements.logisticsCostInput.value || "0");

  const activeIds = getActiveProductIds();
  if (activeIds.length) {
    query.set("productIds", activeIds.join(","));
  }

  return query;
}

function getActiveProductIds() {
  const customIds = parseProductIds(elements.productIdsInput.value);
  if (customIds.length > 0) {
    return customIds;
  }

  return (state.config?.trackedProducts || []).map((item) => item.productId).filter(Boolean);
}

function parseProductIds(rawValue) {
  return String(rawValue || "")
    .split(/[\s,\n\r\t]+/)
    .map((value) => value.trim())
    .filter(Boolean)
    .filter((value, index, array) => array.findIndex((item) => item.toLowerCase() === value.toLowerCase()) === index);
}

function hydrateFromUrl() {
  const params = new URLSearchParams(window.location.search);
  const rawProductIds = params.get("productIds");
  if (rawProductIds) {
    elements.productIdsInput.value = rawProductIds.split(",").join(", ");
  }

  const store = params.get("store");
  if (store) {
    elements.storeSelect.dataset.pendingValue = store;
  }

  const fromDate = params.get("fromDate");
  if (fromDate) {
    elements.fromDateInput.value = fromDate;
  }

  const toDate = params.get("toDate");
  if (toDate) {
    elements.toDateInput.value = toDate;
  }

  const includeTikTokDiscount = params.get("includeTikTokDiscount");
  if (includeTikTokDiscount) {
    elements.includeTikTokDiscountToggle.checked = includeTikTokDiscount === "true";
  }

  const includeBuyerShippingFee = params.get("includeBuyerShippingFee");
  if (includeBuyerShippingFee) {
    elements.includeBuyerShippingFeeToggle.checked = includeBuyerShippingFee === "true";
  }

  const deductPlatformFee = params.get("deductPlatformFee");
  if (deductPlatformFee) {
    elements.deductPlatformFeeToggle.checked = deductPlatformFee === "true";
  }

  const deductLogisticsCost = params.get("deductLogisticsCost");
  if (deductLogisticsCost) {
    elements.deductLogisticsCostToggle.checked = deductLogisticsCost === "true";
  }

  const platformFeeRate = params.get("platformFeeRate");
  if (platformFeeRate) {
    elements.platformFeeRateInput.value = platformFeeRate;
  }

  const logisticsCostPerOrder = params.get("logisticsCostPerOrder");
  if (logisticsCostPerOrder) {
    elements.logisticsCostInput.value = logisticsCostPerOrder;
  }

  if (fromDate || toDate) {
    state.activePreset = "custom";
    updatePresetButtons();
    elements.monthSelect.value = "custom";
  }
}

function renderMonthOptions() {
  const now = new Date();
  const options = ['<option value="">选择月份</option>'];
  for (let i = 0; i < 24; i += 1) {
    const date = new Date(now.getFullYear(), now.getMonth() - i, 1);
    const value = `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}`;
    options.push(`<option value="${value}">${value}</option>`);
  }

  options.push('<option value="custom">自定义日期</option>');
  elements.monthSelect.innerHTML = options.join("");
}

function renderStoreOptions() {
  const stores = state.config?.stores || [];
  elements.storeSelect.innerHTML = ['<option value="all">全部店铺</option>']
    .concat(stores.map((store) => `<option value="${escapeHtml(store.key)}">${escapeHtml(store.name)}</option>`))
    .join("");

  const pendingValue = elements.storeSelect.dataset.pendingValue;
  if (pendingValue && Array.from(elements.storeSelect.options).some((option) => option.value === pendingValue)) {
    elements.storeSelect.value = pendingValue;
  }
}

function renderTrackedProductChips() {
  const trackedProducts = state.config?.trackedProducts || [];
  if (!trackedProducts.length) {
    elements.trackedProductChips.innerHTML = '<span class="muted-pill">当前还没有配置固定 Product ID。</span>';
    return;
  }

  const activeIds = new Set(getActiveProductIds().map((item) => item.toLowerCase()));
  elements.trackedProductChips.innerHTML = trackedProducts
    .map((item) => {
      const selected = activeIds.has(String(item.productId).toLowerCase()) ? "selected" : "";
      return `<button type="button" class="chip-button ${selected}" data-product-id="${escapeHtml(item.productId)}">${escapeHtml(item.label || item.productId)}</button>`;
    })
    .join("");

  elements.trackedProductChips.querySelectorAll("[data-product-id]").forEach((button) => {
    button.addEventListener("click", async () => {
      toggleTrackedProduct(button.dataset.productId || "");
      renderTrackedProductChips();
      renderActiveProductChips();
      await loadSummary();
    });
  });
}

function renderActiveProductChips() {
  const customIds = parseProductIds(elements.productIdsInput.value);
  const usingDefault = customIds.length === 0;
  const activeIds = usingDefault ? getActiveProductIds() : customIds;

  if (usingDefault) {
    elements.activeProductHint.textContent = "当前未输入时，默认按固定的 5 个 Product ID 统计。";
  } else {
    elements.activeProductHint.textContent = "当前按你输入的 Product ID 查询。点击下方标签可以快速移除。";
  }

  if (!activeIds.length) {
    elements.activeProductChips.innerHTML = '<span class="muted-pill">当前没有可查询的 Product ID。</span>';
    return;
  }

  elements.activeProductChips.innerHTML = activeIds
    .map((productId) => {
      const known = (state.config?.trackedProducts || []).find((item) => item.productId === productId);
      const label = known?.label || productId;
      const removable = usingDefault ? "" : `<button type="button" class="chip-remove" data-remove-product-id="${escapeHtml(productId)}">×</button>`;
      return `<span class="product-id-tag">${escapeHtml(label)}${removable}</span>`;
    })
    .join("");

  if (!usingDefault) {
    elements.activeProductChips.querySelectorAll("[data-remove-product-id]").forEach((button) => {
      button.addEventListener("click", async () => {
        removeProductId(button.dataset.removeProductId || "");
        renderTrackedProductChips();
        renderActiveProductChips();
        await loadSummary();
      });
    });
  }
}

function toggleTrackedProduct(productId) {
  if (!productId) {
    return;
  }

  const hasCustomInput = parseProductIds(elements.productIdsInput.value).length > 0;
  if (!hasCustomInput) {
    elements.productIdsInput.value = productId;
    return;
  }

  const activeIds = parseProductIds(elements.productIdsInput.value);
  const nextIds = activeIds.slice();
  const existingIndex = nextIds.findIndex((item) => item.toLowerCase() === productId.toLowerCase());
  if (existingIndex >= 0) {
    nextIds.splice(existingIndex, 1);
  } else {
    nextIds.push(productId);
  }

  elements.productIdsInput.value = nextIds.join(", ");
}

function removeProductId(productId) {
  const nextIds = parseProductIds(elements.productIdsInput.value).filter((item) => item.toLowerCase() !== productId.toLowerCase());
  elements.productIdsInput.value = nextIds.join(", ");
}

function renderSummary() {
  const summary = state.summary;
  if (!summary) {
    return;
  }

  elements.generatedAtChip.textContent = `最近更新 ${formatDateTime(summary.generatedAtUtc)}`;
  renderOverviewCards(summary);
  renderProductCards(summary);
}

function renderOverviewCards(summary) {
  const totals = summary.totals || {};
  elements.overviewCards.innerHTML = [
    createOverviewCard("追踪产品数", totals.productCount, "当前查询范围内命中的 Product ID 数量。"),
    createOverviewCard("涉及订单数", totals.orderCount, "按产品口径累计的成交订单数。"),
    createOverviewCard("实际支付", formatMoney(totals.paidAmount, summary.currency), "只统计已支付且计入销售的金额。", "accent"),
    createOverviewCard("TikTok 折扣", formatMoney(totals.tikTokDiscountAmount, summary.currency), "平台承担的 TikTok 折扣。"),
    createOverviewCard("预估可回款", formatMoney(totals.estimatedReceivableAmount, summary.currency), "已按当前开关扣除平台佣金和物流。", "accent"),
    createOverviewCard("已回款", formatMoney(totals.estimatedSettledReceivableAmount, summary.currency), "状态已完结或可视作已回款的部分。"),
    createOverviewCard("未回款", formatMoney(totals.estimatedPendingReceivableAmount, summary.currency), "已支付但仍待结算的部分。", "warning"),
    createOverviewCard(
      "回款完成率",
      formatPercent(totals.settlementCompletionRate),
      `${totals.completedOrderCount || 0} 单已回款，${totals.pendingSettlementOrderCount || 0} 单未回款。`
    )
  ].join("");
}

function renderProductCards(summary) {
  const rows = summary.products || [];
  if (!rows.length) {
    elements.productPerformanceGrid.innerHTML = '<div class="empty-panel">当前区间没有这批 Product ID 的成交数据。</div>';
    return;
  }

  elements.productPerformanceGrid.innerHTML = rows.map((item) => `
    <article class="panel module performance-card">
      <div class="panel-head">
        <div>
          <p class="eyebrow">Product ID</p>
          <h2>${escapeHtml(item.label || item.productId)}</h2>
          <p>${escapeHtml(item.productName || item.productId)}</p>
        </div>
        <div class="score-pill">
          <span>预估可回款</span>
          <strong>${formatMoney(item.estimatedReceivableAmount, summary.currency)}</strong>
        </div>
      </div>
      <div class="product-month-meta">
        <span>Product ID：${escapeHtml(item.productId)}</span>
        <span>店铺数：${item.storeCount || 0}</span>
        <span>SKU 数：${item.skuCount || 0}</span>
        <span>回款完成率：${formatPercent(item.settlementCompletionRate)}</span>
      </div>
      <div class="product-month-grid">
        <div><span>实际支付</span><strong>${formatMoney(item.paidAmount, summary.currency)}</strong></div>
        <div><span>TikTok 折扣</span><strong>${formatMoney(item.tikTokDiscountAmount, summary.currency)}</strong></div>
        <div><span>买家运费</span><strong>${formatMoney(item.buyerShippingFeeAmount, summary.currency)}</strong></div>
        <div><span>扣平台佣金</span><strong>${formatMoney(item.estimatedPlatformFeeAmount, summary.currency)}</strong></div>
        <div><span>扣物流</span><strong>${formatMoney(item.estimatedLogisticsCostAmount, summary.currency)}</strong></div>
        <div><span>已回款</span><strong>${formatMoney(item.estimatedSettledReceivableAmount, summary.currency)}</strong></div>
        <div><span>未回款</span><strong>${formatMoney(item.estimatedPendingReceivableAmount, summary.currency)}</strong></div>
        <div><span>成交额</span><strong>${formatMoney(item.grossWithDiscount, summary.currency)}</strong></div>
        <div><span>订单数</span><strong>${item.orderCount}</strong></div>
        <div><span>件数</span><strong>${item.quantity || 0}</strong></div>
        <div><span>已回款单数</span><strong>${item.completedOrderCount || 0}</strong></div>
        <div><span>未回款单数</span><strong>${item.pendingSettlementOrderCount || 0}</strong></div>
      </div>
      <div class="product-detail-section">
        <h3>店铺拆分</h3>
        <div class="mini-table">
          ${(item.storeBreakdown || []).map((store) => `
            <div class="mini-row performance-store-row">
              <span>${escapeHtml(store.storeName)}</span>
              <span>${store.orderCount} 单 / ${store.quantity || 0} 件</span>
              <strong>${formatMoney(store.estimatedReceivableAmount, summary.currency)}</strong>
            </div>
          `).join("") || '<div class="empty-panel compact">暂无店铺拆分</div>'}
        </div>
      </div>
      <div class="product-detail-section">
        <h3>按月趋势</h3>
        <div class="performance-chart" data-monthly-chart="${escapeHtml(item.productId)}"></div>
      </div>
      <div class="product-detail-section">
        <h3>按日趋势</h3>
        <div class="performance-chart" data-daily-chart="${escapeHtml(item.productId)}"></div>
      </div>
    </article>
  `).join("");

  rows.forEach((item) => {
    const monthlyTarget = elements.productPerformanceGrid.querySelector(`[data-monthly-chart="${cssEscape(item.productId)}"]`);
    const dailyTarget = elements.productPerformanceGrid.querySelector(`[data-daily-chart="${cssEscape(item.productId)}"]`);

    if (monthlyTarget) {
      renderPerformanceColumnChart(item.monthly || [], monthlyTarget, summary.currency);
    }

    if (dailyTarget) {
      renderPerformanceTrendChart(item.daily || [], dailyTarget, summary.currency);
    }
  });
}

function createOverviewCard(label, value, note, variant = "") {
  return `
    <article class="overview-card ${variant}">
      <span>${escapeHtml(label)}</span>
      <strong>${escapeHtml(String(value))}</strong>
      <small>${escapeHtml(note)}</small>
    </article>
  `;
}

function setDatePreset(preset) {
  state.activePreset = preset;
  const now = new Date();
  let from = new Date(now);
  let to = new Date(now);

  if (preset === "today") {
    elements.monthSelect.value = "custom";
  } else if (preset === "month") {
    from = new Date(now.getFullYear(), now.getMonth(), 1);
    elements.monthSelect.value = `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}`;
  } else if (preset === "7days") {
    from.setDate(from.getDate() - 6);
    elements.monthSelect.value = "custom";
  } else if (preset === "30days") {
    from.setDate(from.getDate() - 29);
    elements.monthSelect.value = "custom";
  }

  elements.fromDateInput.value = toInputDate(from);
  elements.toDateInput.value = toInputDate(to);
  updatePresetButtons();
}

function setMonthRange(monthValue) {
  const [yearText, monthText] = monthValue.split("-");
  const year = Number(yearText);
  const monthIndex = Number(monthText) - 1;
  const start = new Date(year, monthIndex, 1);
  const end = new Date(year, monthIndex + 1, 0);
  elements.fromDateInput.value = toInputDate(start);
  elements.toDateInput.value = toInputDate(end);
  state.activePreset = "custom";
  updatePresetButtons();
}

function updatePresetButtons() {
  elements.presetButtons.forEach((button) => {
    button.classList.toggle("active", button.dataset.preset === state.activePreset);
  });
}

function toInputDate(date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function setStatus(message, isError) {
  elements.statusChip.textContent = message;
  elements.statusChip.classList.toggle("status-error", Boolean(isError));
}

function formatMoney(value, currency) {
  const amount = Number(value || 0);
  const locale = currency === "JPY" ? "ja-JP" : "zh-CN";
  return new Intl.NumberFormat(locale, {
    style: "currency",
    currency: currency || "JPY",
    maximumFractionDigits: currency === "JPY" ? 0 : 2
  }).format(amount);
}

function formatPercent(value) {
  return `${Number(value || 0).toFixed(1)}%`;
}

function formatDateTime(value) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")} ${String(date.getHours()).padStart(2, "0")}:${String(date.getMinutes()).padStart(2, "0")}`;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

function cssEscape(value) {
  if (window.CSS?.escape) {
    return window.CSS.escape(value);
  }

  return String(value ?? "").replaceAll('"', '\\"');
}

function renderPerformanceColumnChart(items, target, currency) {
  if (!items.length) {
    target.innerHTML = '<div class="empty-panel compact">暂无按月趋势</div>';
    return;
  }

  const max = Math.max(...items.map((item) => Number(item.estimatedReceivableAmount || 0)), 1);
  target.innerHTML = `
    <div class="column-grid performance-column-grid">
      ${items.map((item) => {
        const percent = (Number(item.estimatedReceivableAmount || 0) / max) * 100;
        return `
          <article class="column-card">
            <div class="column-value">
              <strong>${formatMoney(item.estimatedReceivableAmount, currency)}</strong>
              <span>${item.orderCount} 单</span>
            </div>
            <div class="column-track">
              <span class="column-fill" style="height:${Math.max(percent, 6)}%"></span>
            </div>
            <div class="column-label">${escapeHtml(item.month)}</div>
          </article>`;
      }).join("")}
    </div>
  `;
}

function renderPerformanceTrendChart(items, target, currency) {
  if (!items.length) {
    target.innerHTML = '<div class="empty-panel compact">暂无按日趋势</div>';
    return;
  }

  const width = 760;
  const height = 240;
  const left = 42;
  const right = 16;
  const top = 18;
  const bottom = 28;
  const chartWidth = width - left - right;
  const chartHeight = height - top - bottom;
  const max = Math.max(...items.flatMap((item) => [
    Number(item.paidAmount || 0),
    Number(item.tikTokDiscountAmount || 0),
    Number(item.estimatedReceivableAmount || 0)
  ]), 1);
  const stepX = items.length === 1 ? 0 : chartWidth / (items.length - 1);

  const series = [
    { key: "paidAmount", color: "#0a7f6f", label: "实付" },
    { key: "tikTokDiscountAmount", color: "#d48f1f", label: "TikTok折扣" },
    { key: "estimatedReceivableAmount", color: "#0f5d75", label: "预估回款" }
  ];

  const paths = series.map((seriesItem) => {
    const points = items.map((item, index) => {
      const value = Number(item[seriesItem.key] || 0);
      return {
        x: left + stepX * index,
        y: top + chartHeight - (value / max) * chartHeight
      };
    });

    return {
      ...seriesItem,
      points,
      path: points.map((point, index) => `${index === 0 ? "M" : "L"} ${point.x} ${point.y}`).join(" ")
    };
  });

  const yLabels = [1, 0.75, 0.5, 0.25, 0].map((ratio) => {
    const value = max * ratio;
    const y = top + chartHeight - chartHeight * ratio;
    return { value, y };
  });

  target.innerHTML = `
    <svg class="chart-svg" viewBox="0 0 ${width} ${height}" preserveAspectRatio="xMidYMid meet">
      ${yLabels.map((item) => `
        <line class="chart-grid" x1="${left}" y1="${item.y}" x2="${width - right}" y2="${item.y}"></line>
        <text class="chart-label" x="4" y="${item.y + 4}">${escapeHtml(shortMoney(item.value, currency))}</text>`).join("")}
      ${paths.map((seriesItem) => `<path class="chart-line" d="${seriesItem.path}" stroke="${seriesItem.color}"></path>`).join("")}
      ${paths.map((seriesItem) => seriesItem.points.map((point) => `<circle class="chart-dot" cx="${point.x}" cy="${point.y}" r="4" fill="${seriesItem.color}"></circle>`).join("")).join("")}
      ${items.map((item, index) => {
        const label = String(item.date || "").slice(5);
        return `<text class="chart-label" x="${left + stepX * index}" y="${height - 8}" text-anchor="middle">${escapeHtml(label)}</text>`;
      }).join("")}
    </svg>
    <div class="chart-legend">
      ${series.map((item) => `<span><i style="background:${item.color}"></i>${escapeHtml(item.label)}</span>`).join("")}
    </div>
  `;
}

function shortMoney(value, currency) {
  const amount = Number(value || 0);
  if (currency === "JPY") {
    return `${Math.round(amount).toLocaleString("ja-JP")}円`;
  }

  return amount.toFixed(0);
}
