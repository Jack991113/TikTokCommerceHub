const state = {
  config: null,
  summary: null,
  overrides: [],
  activePreset: "month"
};

const OVERRIDE_STORAGE_KEY = "tiktok-streamer-compensation-overrides-v2";

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
  includeTikTokDiscountToggle: document.getElementById("includeTikTokDiscountToggle"),
  includeBuyerShippingFeeToggle: document.getElementById("includeBuyerShippingFeeToggle"),
  deductPlatformFeeToggle: document.getElementById("deductPlatformFeeToggle"),
  deductLogisticsCostToggle: document.getElementById("deductLogisticsCostToggle"),
  platformFeeRateInput: document.getElementById("platformFeeRateInput"),
  logisticsCostInput: document.getElementById("logisticsCostInput"),
  hiddenProcurementCostInput: document.getElementById("hiddenProcurementCostInput"),
  exportXlsxButton: document.getElementById("exportXlsxButton"),
  addStreamerButton: document.getElementById("addStreamerButton"),
  overviewCards: document.getElementById("overviewCards"),
  ruleEditorGrid: document.getElementById("ruleEditorGrid"),
  resetRulesButton: document.getElementById("resetRulesButton"),
  streamerGrid: document.getElementById("streamerGrid"),
  monthlyProfitBody: document.getElementById("monthlyProfitBody"),
  storeBreakdownBody: document.getElementById("storeBreakdownBody")
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

  [
    elements.includeTikTokDiscountToggle,
    elements.includeBuyerShippingFeeToggle,
    elements.deductPlatformFeeToggle,
    elements.deductLogisticsCostToggle
  ].forEach((toggle) => toggle.addEventListener("change", () => loadSummary()));

  [
    elements.platformFeeRateInput,
    elements.logisticsCostInput,
    elements.hiddenProcurementCostInput
  ].forEach((input) => input.addEventListener("change", () => loadSummary()));

  elements.exportXlsxButton.addEventListener("click", () => {
    window.location.href = `/api/streamer-compensation/export.xlsx?${buildQuery().toString()}`;
  });

  elements.resetRulesButton.addEventListener("click", async () => {
    state.overrides = buildInitialOverrides(state.config?.streamers || [], []);
    persistOverrides();
    renderRuleEditors();
    await loadSummary();
  });

  elements.addStreamerButton.addEventListener("click", async () => {
    state.overrides = readRuleOverridesFromEditor();
    state.overrides.push(createCustomStreamerOverride(state.overrides));
    persistOverrides();
    renderRuleEditors();
    focusLastStreamerLabel();
    await loadSummary();
  });
}

async function loadConfig() {
  setStatus("正在读取主播规则...", false);
  const response = await fetch("/api/streamer-compensation/config");
  if (!response.ok) {
    throw new Error("读取主播规则失败");
  }

  state.config = await response.json();
  elements.timezoneChip.textContent = `时区 ${state.config.timezone || "-"}`;
  renderStoreOptions();

  const storedOverrides = loadStoredOverrides();
  state.overrides = buildInitialOverrides(state.config.streamers || [], storedOverrides);
  persistOverrides();
  renderRuleEditors();
}

async function loadSummary() {
  setStatus("正在计算主播薪资与利润...", false);
  const response = await fetch(`/api/streamer-compensation/summary?${buildQuery().toString()}`);
  if (!response.ok) {
    throw new Error("读取主播薪资数据失败");
  }

  state.summary = await response.json();
  renderSummary();
  setStatus("主播薪资与利润已更新", false);
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
  query.set("hiddenProcurementCostJpy", elements.hiddenProcurementCostInput.value || "0");
  query.set("ruleOverrides", JSON.stringify(readRuleOverridesForApi()));
  return query;
}

function hydrateFromUrl() {
  const params = new URLSearchParams(window.location.search);

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

  const hiddenProcurementCostJpy = params.get("hiddenProcurementCostJpy");
  if (hiddenProcurementCostJpy) {
    elements.hiddenProcurementCostInput.value = hiddenProcurementCostJpy;
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

  for (let index = 0; index < 24; index += 1) {
    const date = new Date(now.getFullYear(), now.getMonth() - index, 1);
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

function renderRuleEditors() {
  if (!state.overrides.length) {
    elements.ruleEditorGrid.innerHTML = '<span class="muted-pill">当前还没有主播规则</span>';
    return;
  }

  elements.ruleEditorGrid.innerHTML = state.overrides
    .map((rule) => {
      const productText = (rule.productIds || []).join("\n");
      return `
        <article class="link-rule-card compact-rule streamer-rule-card" data-streamer-rule-card="${escapeHtml(rule.key)}">
          <div class="panel-head">
            <div>
              <h3 class="mini-title">${escapeHtml(rule.label || rule.key)}</h3>
              <p>${rule.isCustom ? "自定义主播规则" : "默认主播规则，可直接改底薪、提成和 Product ID"}</p>
            </div>
            <div class="panel-actions">
              <span class="status-chip subtle">${(rule.productIds || []).length} 个 Product ID</span>
              ${rule.isCustom ? '<button type="button" class="ghost-button streamer-remove-button">删除</button>' : ""}
            </div>
          </div>
          <div class="streamer-rule-form-grid">
            <label class="field compact">
              <span>主播名称</span>
              <input class="streamer-label-input" type="text" value="${escapeHtml(rule.label || "")}" placeholder="例如 Ruby" />
            </label>
            <label class="field compact">
              <span>底薪 (RMB)</span>
              <input class="streamer-base-salary" type="number" min="0" step="1" value="${escapeHtml(String(rule.baseSalaryAmount ?? 0))}" />
            </label>
            <label class="field compact">
              <span>提成比例 (%)</span>
              <input class="streamer-commission-rate" type="number" min="0" step="0.1" value="${escapeHtml(formatRatePercentInput(rule.commissionRate ?? 0))}" />
            </label>
          </div>
          <label class="field">
            <span>备注</span>
            <input class="streamer-note-input" type="text" value="${escapeHtml(rule.note || "")}" placeholder="例如 3% 提成，不含折扣" />
          </label>
          <label class="field">
            <span>Product ID / 链接映射</span>
            <textarea class="streamer-product-ids-input" rows="4" placeholder="一行一个，或用逗号分隔">${escapeHtml(productText)}</textarea>
            <small>支持多个 Product ID，会自动按这些 Product ID 聚合销量与薪资。</small>
          </label>
        </article>`;
    })
    .join("");

  bindRuleEditorEvents();
}

function bindRuleEditorEvents() {
  elements.ruleEditorGrid.querySelectorAll(".streamer-label-input,.streamer-note-input,.streamer-product-ids-input,.streamer-base-salary,.streamer-commission-rate")
    .forEach((input) => {
      const eventName = input.classList.contains("streamer-product-ids-input") || input.classList.contains("streamer-note-input") ? "change" : "input";
      input.addEventListener(eventName, syncRuleEditorState);
      if (eventName !== "change") {
        input.addEventListener("change", syncRuleEditorState);
      }
    });

  elements.ruleEditorGrid.querySelectorAll(".streamer-remove-button").forEach((button) => {
    button.addEventListener("click", async (event) => {
      const card = event.currentTarget.closest("[data-streamer-rule-card]");
      const key = card?.getAttribute("data-streamer-rule-card");
      if (!key) {
        return;
      }

      state.overrides = readRuleOverridesFromEditor().filter((item) => item.key !== key);
      persistOverrides();
      renderRuleEditors();
      await loadSummary();
    });
  });
}

async function syncRuleEditorState() {
  state.overrides = readRuleOverridesFromEditor();
  persistOverrides();
  await loadSummary();
}

function renderSummary() {
  const summary = state.summary;
  if (!summary) {
    return;
  }

  elements.generatedAtChip.textContent = `最近更新 ${formatDateTime(summary.generatedAtUtc)}`;
  renderOverviewCards(summary);
  renderStreamerCards(summary);
  renderMonthlyProfit(summary.monthlyProfit || [], summary.currency || "JPY");
  renderStoreBreakdown(summary);
}

function renderOverviewCards(summary) {
  const totals = summary.totals || {};
  const currency = summary.currency || "JPY";
  const cards = [
    {
      label: "实际支付",
      value: formatMoney(totals.paidAmount || 0, currency),
      note: `${totals.orderCount || 0} 单 / ${totals.quantity || 0} 件`
    },
    {
      label: "折扣回款(归你)",
      value: formatMoney(totals.tikTokDiscountAmount || 0, currency),
      note: "不计入主播提成"
    },
    {
      label: "预估可回款",
      value: formatMoney(totals.estimatedReceivableAmount || 0, currency),
      note: `已回款 ${formatMoney(totals.estimatedSettledReceivableAmount || 0, currency)}`
    },
    {
      label: "未回款",
      value: formatMoney(totals.estimatedPendingReceivableAmount || 0, currency),
      note: `回款完成率 ${formatPercent(totals.settlementCompletionRate || 0)}`
    },
    {
      label: "主播实发薪资",
      value: formatMoney(totals.salaryTotalAmountRmb || 0, "RMB"),
      note: `底薪 ${formatMoney(totals.salaryBaseAmountRmb || 0, "RMB")} / 提成 ${formatMoney(totals.salaryCommissionAmountJpy || 0, "JPY")}`
    },
    {
      label: "利润后",
      value: formatMoney(totals.profitAfterHiddenCostJpy || 0, "JPY"),
      note: `隐形成本 ${formatMoney(summary.hiddenProcurementCostJpy || 0, "JPY")}`
    }
  ];

  elements.overviewCards.innerHTML = cards
    .map((card) => `
      <article class="overview-card">
        <span>${escapeHtml(card.label)}</span>
        <strong>${escapeHtml(card.value)}</strong>
        <small>${escapeHtml(card.note)}</small>
      </article>`)
    .join("");
}

function renderStreamerCards(summary) {
  const currency = summary.currency || "JPY";
  const entries = [...(summary.streamers || []), summary.selfOwned].filter(Boolean);

  if (!entries.length) {
    elements.streamerGrid.innerHTML = '<div class="empty-panel">当前区间没有主播或自营数据。</div>';
    return;
  }

  elements.streamerGrid.innerHTML = entries
    .map((entry) => {
      const monthlyRows = (entry.monthly || [])
        .map((month) => `
          <tr>
            <td>${escapeHtml(month.month || "-")}</td>
            <td>${escapeHtml(formatMoney(month.paidAmount || 0, currency))}</td>
            <td>${escapeHtml(formatMoney(month.baseSalaryAmount || 0, "RMB"))}</td>
            <td>${escapeHtml(formatMoney(month.commissionAmountJpy || 0, "JPY"))}</td>
            <td>${escapeHtml(formatMoney(month.salaryTotalAmountRmb || 0, "RMB"))}</td>
            <td>${escapeHtml(formatMoney(month.profitBeforeHiddenCostJpy || 0, "JPY"))}</td>
          </tr>`)
        .join("");

      const storeLines = (entry.storeBreakdown || [])
        .map((store) => `
          <div class="metric-row">
            <strong>${escapeHtml(store.storeName || "-")}</strong>
            <span>${escapeHtml(formatMoney(store.paidAmount || 0, currency))} / 回款 ${escapeHtml(formatMoney(store.estimatedReceivableAmount || 0, currency))}</span>
          </div>`)
        .join("");

      return `
        <article class="panel module streamer-card">
          <div class="panel-head">
            <div>
              <h2>${escapeHtml(entry.label || "-")}</h2>
              <p>${escapeHtml(entry.note || (entry.isSelfOwned ? "未分配给主播的链接自动归为自营。" : "提成只按实际支付计算，折扣回款归你自己。"))}</p>
            </div>
            <span class="status-chip subtle">${entry.isSelfOwned ? "自营" : `提成 ${escapeHtml(entry.commissionLabel || "-")}`}</span>
          </div>
          <div class="metric-grid">
            <div class="status-card"><span>实际支付</span><strong>${escapeHtml(formatMoney(entry.paidAmount || 0, currency))}</strong><small>${entry.orderCount || 0} 单 / ${entry.quantity || 0} 件</small></div>
            <div class="status-card"><span>折扣回款(归你)</span><strong>${escapeHtml(formatMoney(entry.tikTokDiscountAmount || 0, currency))}</strong><small>不计主播提成</small></div>
            <div class="status-card"><span>预估可回款</span><strong>${escapeHtml(formatMoney(entry.estimatedReceivableAmount || 0, currency))}</strong><small>已回款 ${escapeHtml(formatMoney(entry.estimatedSettledReceivableAmount || 0, currency))}</small></div>
            <div class="status-card"><span>底薪</span><strong>${escapeHtml(formatMoney(entry.baseSalaryAmount || 0, "RMB"))}</strong><small>${escapeHtml(entry.baseSalaryCurrency || "RMB")}</small></div>
            <div class="status-card"><span>提成</span><strong>${escapeHtml(formatMoney(entry.commissionAmountJpy || 0, "JPY"))}</strong><small>${escapeHtml(entry.commissionLabel || "0%")}</small></div>
            <div class="status-card"><span>实发薪资</span><strong>${escapeHtml(formatMoney(entry.salaryTotalAmountRmb || 0, "RMB"))}</strong><small>底薪 + 提成</small></div>
            <div class="status-card"><span>利润后</span><strong>${escapeHtml(formatMoney(entry.profitAfterHiddenCostJpy || 0, "JPY"))}</strong><small>分摊隐形成本 ${escapeHtml(formatMoney(entry.allocatedHiddenProcurementCostJpy || 0, "JPY"))}</small></div>
          </div>
          <div class="tag-list">
            ${(entry.productIds || []).map((product) => `<span class="product-id-tag">${escapeHtml(product.productId || "-")}</span>`).join("")}
          </div>
          <div class="dashboard-grid compact-grid">
            <section>
              <h3 class="mini-title">店铺拆分</h3>
              <div class="metric-list">${storeLines || '<div class="empty-panel compact">当前区间没有店铺拆分数据</div>'}</div>
            </section>
            <section>
              <h3 class="mini-title">月度薪资</h3>
              <div class="data-table-wrap compact-table">
                <table class="data-table compact">
                  <thead>
                    <tr>
                      <th>月份</th>
                      <th>实际支付</th>
                      <th>底薪 RMB</th>
                      <th>提成 JPY</th>
                      <th>实发薪资 RMB</th>
                      <th>利润前 JPY</th>
                    </tr>
                  </thead>
                  <tbody>${monthlyRows || '<tr><td colspan="6" class="empty-cell">当前区间没有月度数据</td></tr>'}</tbody>
                </table>
              </div>
            </section>
          </div>
        </article>`;
    })
    .join("");
}

function renderMonthlyProfit(rows, currency) {
  if (!rows.length) {
    elements.monthlyProfitBody.innerHTML = '<tr><td colspan="10" class="empty-cell">当前区间没有月度利润数据</td></tr>';
    return;
  }

  elements.monthlyProfitBody.innerHTML = rows
    .map((item) => `
      <tr>
        <td>${escapeHtml(item.month || "-")}</td>
        <td>${escapeHtml(formatMoney(item.paidAmount || 0, currency))}</td>
        <td>${escapeHtml(formatMoney(item.tikTokDiscountAmount || 0, currency))}</td>
        <td>${escapeHtml(formatMoney(item.estimatedReceivableAmount || 0, currency))}</td>
        <td>${escapeHtml(formatMoney(item.salaryBaseAmountRmb || 0, "RMB"))}</td>
        <td>${escapeHtml(formatMoney(item.salaryCommissionAmountJpy || 0, "JPY"))}</td>
        <td>${escapeHtml(formatMoney(item.salaryTotalAmountRmb || 0, "RMB"))}</td>
        <td>${escapeHtml(formatMoney(item.hiddenProcurementCostJpy || 0, "JPY"))}</td>
        <td>${escapeHtml(formatMoney(item.profitBeforeHiddenCostJpy || 0, "JPY"))}</td>
        <td>${escapeHtml(formatMoney(item.profitAfterHiddenCostJpy || 0, "JPY"))}</td>
      </tr>`)
    .join("");
}

function renderStoreBreakdown(summary) {
  const rows = [...(summary.streamers || []), summary.selfOwned]
    .filter(Boolean)
    .flatMap((entry) => (entry.storeBreakdown || []).map((store) => ({
      label: entry.label,
      storeName: store.storeName,
      orderCount: store.orderCount,
      paidAmount: store.paidAmount,
      receivable: store.estimatedReceivableAmount,
      settled: store.estimatedSettledReceivableAmount,
      pending: store.estimatedPendingReceivableAmount
    })));

  if (!rows.length) {
    elements.storeBreakdownBody.innerHTML = '<tr><td colspan="7" class="empty-cell">当前区间没有店铺拆分数据</td></tr>';
    return;
  }

  const currency = summary.currency || "JPY";
  elements.storeBreakdownBody.innerHTML = rows
    .map((row) => `
      <tr>
        <td>${escapeHtml(row.label || "-")}</td>
        <td>${escapeHtml(row.storeName || "-")}</td>
        <td>${escapeHtml(String(row.orderCount || 0))}</td>
        <td>${escapeHtml(formatMoney(row.paidAmount || 0, currency))}</td>
        <td>${escapeHtml(formatMoney(row.receivable || 0, currency))}</td>
        <td>${escapeHtml(formatMoney(row.settled || 0, currency))}</td>
        <td>${escapeHtml(formatMoney(row.pending || 0, currency))}</td>
      </tr>`)
    .join("");
}

function buildInitialOverrides(configRules, storedOverrides) {
  const storedLookup = new Map((storedOverrides || []).filter((item) => item?.key).map((item) => [item.key, item]));
  const merged = configRules.map((rule) => mergeRuleForEditor(rule, storedLookup.get(rule.key), false));

  for (const stored of storedOverrides || []) {
    if (!stored?.key || configRules.some((rule) => rule.key === stored.key)) {
      continue;
    }

    merged.push(mergeRuleForEditor(null, stored, true));
  }

  return merged;
}

function mergeRuleForEditor(configRule, storedRule, forceCustom) {
  const key = storedRule?.key || configRule?.key || `custom-${Date.now()}`;
  const productIds = sanitizeProductIds(storedRule?.productIds?.length ? storedRule.productIds : configRule?.productIds || []);

  return {
    key,
    label: sanitizeLabel(storedRule?.label || configRule?.label || key),
    note: String(storedRule?.note || configRule?.note || "").trim(),
    productIds,
    baseSalaryAmount: Number.isFinite(Number(storedRule?.baseSalaryAmount)) ? Number(storedRule.baseSalaryAmount) : Number(configRule?.baseSalaryAmount || 0),
    commissionRate: Number.isFinite(Number(storedRule?.commissionRate)) ? Number(storedRule.commissionRate) : Number(configRule?.commissionRate || 0),
    isCustom: Boolean(forceCustom || storedRule?.isCustom || !configRule)
  };
}

function createCustomStreamerOverride(existing) {
  const sequence = (existing || []).filter((item) => item.isCustom).length + 1;
  return {
    key: `custom-${Date.now()}-${sequence}`,
    label: `新主播 ${sequence}`,
    note: "",
    productIds: [],
    baseSalaryAmount: 0,
    commissionRate: 0,
    isCustom: true
  };
}

function focusLastStreamerLabel() {
  const inputs = elements.ruleEditorGrid.querySelectorAll(".streamer-label-input");
  const lastInput = inputs[inputs.length - 1];
  if (lastInput) {
    lastInput.focus();
    lastInput.select();
  }
}

function loadStoredOverrides() {
  try {
    const raw = window.localStorage.getItem(OVERRIDE_STORAGE_KEY);
    if (!raw) {
      return [];
    }

    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

function persistOverrides() {
  window.localStorage.setItem(OVERRIDE_STORAGE_KEY, JSON.stringify(state.overrides));
}

function readRuleOverridesFromEditor() {
  if (!elements.ruleEditorGrid?.children?.length) {
    return state.overrides.slice();
  }

  return Array.from(elements.ruleEditorGrid.querySelectorAll("[data-streamer-rule-card]")).map((card) => {
    const key = card.getAttribute("data-streamer-rule-card") || "";
    const isCustom = card.classList.contains("streamer-rule-card") && card.querySelector(".streamer-remove-button") !== null;
    const label = sanitizeLabel(card.querySelector(".streamer-label-input")?.value || key);
    const note = String(card.querySelector(".streamer-note-input")?.value || "").trim();
    const productIds = sanitizeProductIds(card.querySelector(".streamer-product-ids-input")?.value || "");
    const baseSalaryAmount = Math.max(0, Number(card.querySelector(".streamer-base-salary")?.value || 0));
    const commissionPercent = Math.max(0, Number(card.querySelector(".streamer-commission-rate")?.value || 0));

    return {
      key,
      label,
      note,
      productIds,
      baseSalaryAmount: Number.isFinite(baseSalaryAmount) ? baseSalaryAmount : 0,
      commissionRate: Number.isFinite(commissionPercent) ? commissionPercent / 100 : 0,
      isCustom
    };
  });
}

function readRuleOverridesForApi() {
  return readRuleOverridesFromEditor().map((item) => ({
    key: item.key,
    label: item.label,
    note: item.note,
    productIds: sanitizeProductIds(item.productIds),
    baseSalaryAmount: Number(item.baseSalaryAmount || 0),
    commissionRate: Number(item.commissionRate || 0)
  }));
}

function setDatePreset(preset) {
  state.activePreset = preset;
  updatePresetButtons();

  if (preset === "today") {
    const today = formatDateInput(new Date());
    elements.fromDateInput.value = today;
    elements.toDateInput.value = today;
    elements.monthSelect.value = "custom";
    return;
  }

  if (preset === "month") {
    setMonthRange(currentMonthValue());
    return;
  }

  const now = new Date();
  if (preset === "6months") {
    const from = new Date(now.getFullYear(), now.getMonth() - 5, 1);
    elements.fromDateInput.value = formatDateInput(from);
    elements.toDateInput.value = formatDateInput(now);
    elements.monthSelect.value = "custom";
    return;
  }

  const days = preset === "7days" ? 6 : 29;
  const from = new Date(now);
  from.setDate(now.getDate() - days);
  elements.fromDateInput.value = formatDateInput(from);
  elements.toDateInput.value = formatDateInput(now);
  elements.monthSelect.value = "custom";
}

function setMonthRange(monthValue) {
  if (!monthValue || monthValue === "custom") {
    return;
  }

  const [yearText, monthText] = monthValue.split("-");
  const year = Number(yearText);
  const month = Number(monthText);
  if (!year || !month) {
    return;
  }

  const start = new Date(year, month - 1, 1);
  const end = new Date(year, month, 0);
  elements.fromDateInput.value = formatDateInput(start);
  elements.toDateInput.value = formatDateInput(end);
  elements.monthSelect.value = monthValue;
  updatePresetButtons();
}

function currentMonthValue() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}`;
}

function updatePresetButtons() {
  elements.presetButtons.forEach((button) => {
    button.classList.toggle("active", button.dataset.preset === state.activePreset);
  });
}

function sanitizeProductIds(value) {
  const rawValues = Array.isArray(value) ? value : String(value || "").split(/[\n\r,\t ]+/);
  return rawValues
    .map((item) => String(item || "").trim())
    .filter(Boolean)
    .filter((item, index, array) => array.findIndex((candidate) => candidate.toLowerCase() === item.toLowerCase()) === index);
}

function sanitizeLabel(value) {
  return String(value || "").trim() || "未命名主播";
}

function formatRatePercentInput(value) {
  const percent = Number(value || 0) * 100;
  return percent.toFixed(2).replace(/\.00$/, "").replace(/(\.\d)0$/, "$1");
}

function setStatus(message, isError) {
  elements.statusChip.textContent = message;
  elements.statusChip.classList.toggle("status-error", Boolean(isError));
}

function formatMoney(value, currency) {
  const amount = Number(value || 0);
  return `${new Intl.NumberFormat("zh-CN", {
    minimumFractionDigits: currency === "JPY" || currency === "RMB" ? 0 : 2,
    maximumFractionDigits: currency === "JPY" || currency === "RMB" ? 0 : 2
  }).format(amount)} ${currency}`;
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

function formatDateInput(value) {
  const date = new Date(value);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
}

function escapeHtml(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}
