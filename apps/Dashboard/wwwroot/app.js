const state = {
    stores: [],
    summary: null,
    streamerSummary: null,
    derived: null,
    linkRules: [],
    activePreset: "month",
    activeView: "overview",
    activeRequestToken: 0
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
    viewButtons: Array.from(document.querySelectorAll(".view-button")),
    viewPanels: Array.from(document.querySelectorAll("[data-view-panel]")),
    includeTikTokDiscountToggle: document.getElementById("includeTikTokDiscountToggle"),
    includeBuyerShippingFeeToggle: document.getElementById("includeBuyerShippingFeeToggle"),
    deductPlatformFeeToggle: document.getElementById("deductPlatformFeeToggle"),
    deductLogisticsCostToggle: document.getElementById("deductLogisticsCostToggle"),
    platformFeeRateInput: document.getElementById("platformFeeRateInput"),
    logisticsCostInput: document.getElementById("logisticsCostInput"),
    overviewCards: document.getElementById("overviewCards"),
    insightCards: document.getElementById("insightCards"),
    dailyChartMeta: document.getElementById("dailyChartMeta"),
    dailyTrendChart: document.getElementById("dailyTrendChart"),
    monthlyChartMeta: document.getElementById("monthlyChartMeta"),
    monthlyBarChart: document.getElementById("monthlyBarChart"),
    weekdayBreakdown: document.getElementById("weekdayBreakdown"),
    hourlyHeatmap: document.getElementById("hourlyHeatmap"),
    compassScore: document.getElementById("compassScore"),
    businessCompassChart: document.getElementById("businessCompassChart"),
    compassAxisList: document.getElementById("compassAxisList"),
    funnelCards: document.getElementById("funnelCards"),
    dailyTableBody: document.getElementById("dailyTableBody"),
    monthlyTableBody: document.getElementById("monthlyTableBody"),
    storeShareChart: document.getElementById("storeShareChart"),
    storeBreakdown: document.getElementById("storeBreakdown"),
    paymentShareChart: document.getElementById("paymentShareChart"),
    paymentBreakdown: document.getElementById("paymentBreakdown"),
    statusBreakdown: document.getElementById("statusBreakdown"),
    hourlyBreakdown: document.getElementById("hourlyBreakdown"),
    orderBucketChart: document.getElementById("orderBucketChart"),
    handleSourceBreakdown: document.getElementById("handleSourceBreakdown"),
    buyerBreakdown: document.getElementById("buyerBreakdown"),
    buyerSegmentBreakdown: document.getElementById("buyerSegmentBreakdown"),
    topOrders: document.getElementById("topOrders"),
    productMonthQuickInput: document.getElementById("productMonthQuickInput"),
    productMonthQuickButton: document.getElementById("productMonthQuickButton"),
    productMonthQuickClearButton: document.getElementById("productMonthQuickClearButton"),
    productMonthQuickSummary: document.getElementById("productMonthQuickSummary"),
    productBreakdown: document.getElementById("productBreakdown"),
    productIdFilterInput: document.getElementById("productIdFilterInput"),
    productIdSuggestions: document.getElementById("productIdSuggestions"),
    applyProductFilterButton: document.getElementById("applyProductFilterButton"),
    clearProductFilterButton: document.getElementById("clearProductFilterButton"),
    selectedProductChips: document.getElementById("selectedProductChips"),
    productIdBreakdown: document.getElementById("productIdBreakdown"),
    selectedProductDetails: document.getElementById("selectedProductDetails"),
    discountOrderList: document.getElementById("discountOrderList"),
    linkAttributionBreakdown: document.getElementById("linkAttributionBreakdown"),
    linkRuleList: document.getElementById("linkRuleList"),
    addLinkRuleButton: document.getElementById("addLinkRuleButton"),
    saveLinkRulesButton: document.getElementById("saveLinkRulesButton"),
    reconciliationCards: document.getElementById("reconciliationCards"),
    settlementBreakdown: document.getElementById("settlementBreakdown"),
    reconciliationNote: document.getElementById("reconciliationNote"),
    includedOrders: document.getElementById("includedOrders"),
    includedCountLabel: document.getElementById("includedCountLabel"),
    riskCards: document.getElementById("riskCards"),
    unpaidReminderList: document.getElementById("unpaidReminderList"),
    potentialCustomerList: document.getElementById("potentialCustomerList"),
    blacklistList: document.getElementById("blacklistList"),
    excludedOrders: document.getElementById("excludedOrders"),
    excludedCountLabel: document.getElementById("excludedCountLabel"),
    openProductPerformanceButton: document.getElementById("openProductPerformanceButton"),
    openStreamerCompensationButton: document.getElementById("openStreamerCompensationButton"),
    copySummaryButton: document.getElementById("copySummaryButton"),
    copyOrderIdsButton: document.getElementById("copyOrderIdsButton"),
    copyOrderAmountButton: document.getElementById("copyOrderAmountButton"),
    exportCsvButton: document.getElementById("exportCsvButton"),
    exportXlsxButton: document.getElementById("exportXlsxButton"),
    emptyRowTemplate: document.getElementById("emptyRowTemplate")
};

const VIEW_LABELS = {
    overview: "总览",
    rhythm: "节奏",
    commerce: "经营",
    buyers: "客户",
    products: "商品",
    reconciliation: "对账",
    risk: "风险"
};

function ensureLinkAttributionModules() {
    const productsView = document.querySelector('[data-view-panel="products"]');
    if (!productsView || productsView.querySelector("[data-link-attribution-panel]")) {
        return;
    }

    const section = document.createElement("section");
    section.className = "dashboard-grid";
    section.dataset.linkAttributionPanel = "true";
    section.innerHTML = `
        <article class="panel module">
            <div class="panel-head">
                <div>
                    <h2>链接归因排行</h2>
                    <p>按主播链接或商品链接归因后的销售额排行，支持看订单数、买家数和成交额。</p>
                </div>
            </div>
            <div class="rank-list" id="linkAttributionBreakdown"></div>
        </article>
        <article class="panel module">
            <div class="panel-head">
                <div>
                    <h2>链接规则</h2>
                    <p>每个主播一条规则。填主播名和商品链接即可，提不准时再补 SKU 或关键词。</p>
                </div>
                <div class="panel-actions">
                    <button type="button" class="secondary-button" id="addLinkRuleButton">新增规则</button>
                    <button type="button" class="primary-button" id="saveLinkRulesButton">保存规则</button>
                </div>
            </div>
            <div class="link-rule-list" id="linkRuleList"></div>
        </article>`;

    productsView.append(section);
    elements.linkAttributionBreakdown = document.getElementById("linkAttributionBreakdown");
    elements.linkRuleList = document.getElementById("linkRuleList");
    elements.addLinkRuleButton = document.getElementById("addLinkRuleButton");
    elements.saveLinkRulesButton = document.getElementById("saveLinkRulesButton");
}

document.addEventListener("DOMContentLoaded", async () => {
    ensureLinkAttributionModules();
    wireEvents();
    renderMonthOptions();
    setDatePreset("month");
    setActiveView("overview");

    try {
        await loadStores();
        await loadLinkRules();
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

    elements.viewButtons.forEach((button) => {
        button.addEventListener("click", () => setActiveView(button.dataset.view || "overview"));
    });

    [
        elements.includeTikTokDiscountToggle,
        elements.includeBuyerShippingFeeToggle,
        elements.deductPlatformFeeToggle,
        elements.deductLogisticsCostToggle
    ].forEach((toggle) => {
        toggle.addEventListener("change", () => loadSummary());
    });

    [elements.platformFeeRateInput, elements.logisticsCostInput].forEach((input) => {
        input.addEventListener("change", () => loadSummary());
    });

    if (elements.applyProductFilterButton) {
        elements.applyProductFilterButton.addEventListener("click", () => loadSummary());
    }

    if (elements.productMonthQuickButton) {
        elements.productMonthQuickButton.addEventListener("click", async () => {
            await runMonthlyProductLookup();
        });
    }

    if (elements.productMonthQuickClearButton) {
        elements.productMonthQuickClearButton.addEventListener("click", async () => {
            clearMonthlyProductLookup();
            await loadSummary();
        });
    }

    if (elements.clearProductFilterButton) {
        elements.clearProductFilterButton.addEventListener("click", async () => {
            elements.productIdFilterInput.value = "";
            if (elements.productMonthQuickInput) {
                elements.productMonthQuickInput.value = "";
            }
            await loadSummary();
        });
    }

    if (elements.productMonthQuickInput) {
        elements.productMonthQuickInput.addEventListener("keydown", async (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                await runMonthlyProductLookup();
            }
        });
    }

    if (elements.productIdFilterInput) {
        elements.productIdFilterInput.addEventListener("keydown", async (event) => {
            if (event.key === "Enter") {
                event.preventDefault();
                await loadSummary();
            }
        });
    }

    elements.copySummaryButton.addEventListener("click", async () => {
        if (state.summary) {
            await copyText(buildSummaryClipboard(state.summary), "汇总已复制");
        }
    });

    if (elements.openProductPerformanceButton) {
        elements.openProductPerformanceButton.addEventListener("click", () => {
            window.open(`/product-performance.html?${buildQuery().toString()}`, "_blank", "noopener");
        });
    }

    if (elements.openStreamerCompensationButton) {
        elements.openStreamerCompensationButton.addEventListener("click", () => {
            window.open(`/streamer-compensation.html?${buildQuery().toString()}`, "_blank", "noopener");
        });
    }

    elements.copyOrderIdsButton.addEventListener("click", async () => {
        if (state.summary) {
            const fullSummary = await fetchFullSummaryForExport();
            await copyText(buildOrderIdsClipboard(fullSummary), "全部订单号已复制");
        }
    });

    elements.copyOrderAmountButton.addEventListener("click", async () => {
        if (state.summary) {
            const fullSummary = await fetchFullSummaryForExport();
            await copyText(buildOrderAmountsClipboard(fullSummary), "订单号和金额已复制");
        }
    });

    elements.exportCsvButton.addEventListener("click", async () => {
        if (!state.summary) {
            return;
        }

        const fullSummary = await fetchFullSummaryForExport();
        const blob = new Blob([buildCsv(fullSummary)], { type: "text/csv;charset=utf-8;" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        const [from, to] = currentRangeLabels();
        link.href = url;
        link.download = `sales-dashboard-${from}-${to}.csv`;
        link.click();
        URL.revokeObjectURL(url);
    });

    elements.exportXlsxButton.addEventListener("click", () => {
        window.location.href = `/api/sales/export.xlsx?${buildQuery().toString()}`;
    });

    if (elements.addLinkRuleButton) {
        elements.addLinkRuleButton.addEventListener("click", () => {
            state.linkRules.push(createEmptyLinkRule());
            renderLinkRules();
        });
    }

    if (elements.saveLinkRulesButton) {
        elements.saveLinkRulesButton.addEventListener("click", async () => {
            await saveLinkRules();
        });
    }
}

async function loadStores() {
    setStatus("正在读取店铺配置...", false);
    const response = await fetch("/api/stores");
    if (!response.ok) {
        throw new Error("读取店铺配置失败");
    }

    const payload = await response.json();
    state.stores = payload.stores || [];
    elements.timezoneChip.textContent = `时区 ${payload.timezone || "-"}`;
    renderStoreOptions();
}

async function loadLinkRules() {
    const response = await fetch("/api/link-attribution/rules");
    if (!response.ok) {
        throw new Error("读取链接归因规则失败");
    }

    const payload = await response.json();
    state.linkRules = Array.isArray(payload.rules) ? payload.rules : [];
    if (state.linkRules.length === 0) {
        state.linkRules = [createEmptyLinkRule()];
    }

    renderLinkRules();
}

async function saveLinkRules() {
    const payload = {
        rules: readLinkRulesFromEditor()
    };

    const response = await fetch("/api/link-attribution/rules", {
        method: "PUT",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(payload)
    });

    if (!response.ok) {
        throw new Error("保存链接归因规则失败");
    }

    const saved = await response.json();
    state.linkRules = Array.isArray(saved.rules) ? saved.rules : [];
    if (state.linkRules.length === 0) {
        state.linkRules = [createEmptyLinkRule()];
    }

    renderLinkRules();
    await loadSummary();
    setStatus("链接归因规则已保存", false);
}

async function loadSummary() {
    setStatus("正在从 TikTok API 拉取统计数据...", false);
    const requestToken = Date.now();
    state.activeRequestToken = requestToken;
    state.streamerSummary = null;
    const response = await fetch(`/api/sales/summary?${buildQuery().toString()}`);

    if (!response.ok) {
        throw new Error("读取统计数据失败");
    }

    state.summary = await response.json();
    if (state.activeRequestToken !== requestToken) {
        return;
    }
    state.derived = buildDerived(state.summary);
    renderSummary();
    setStatus(`基础数据已更新 · ${VIEW_LABELS[state.activeView] || "总览"}`, false);
    void loadStreamerSummary(requestToken);
}

async function loadStreamerSummary(requestToken) {
    try {
        const response = await fetch(`/api/streamer-compensation/summary?${buildStreamerQuery().toString()}`);
        if (!response.ok) {
            throw new Error("读取主播汇总失败");
        }

        const summary = await response.json();
        if (state.activeRequestToken !== requestToken) {
            return;
        }

        state.streamerSummary = summary;
        if (state.summary) {
            renderActiveView();
        }
        setStatus(`数据已更新 · ${VIEW_LABELS[state.activeView] || "总览"}`, false);
    } catch (error) {
        if (state.activeRequestToken === requestToken) {
            setStatus("基础数据已更新，主播汇总稍后重试", false);
        }
    }
}

function buildQuery(options = {}) {
    const query = new URLSearchParams();
    query.set("store", elements.storeSelect.value || "all");
    const productIds = normalizeProductIdInput(elements.productIdFilterInput?.value || "");
    if (productIds.length) {
        query.set("productIds", productIds.join(","));
    }
    if (elements.fromDateInput.value) query.set("fromDate", elements.fromDateInput.value);
    if (elements.toDateInput.value) query.set("toDate", elements.toDateInput.value);
    query.set("includeTikTokDiscount", elements.includeTikTokDiscountToggle.checked ? "true" : "false");
    query.set("includeBuyerShippingFee", elements.includeBuyerShippingFeeToggle.checked ? "true" : "false");
    query.set("deductPlatformFee", elements.deductPlatformFeeToggle.checked ? "true" : "false");
    query.set("deductLogisticsCost", elements.deductLogisticsCostToggle.checked ? "true" : "false");
    query.set("platformFeeRate", elements.platformFeeRateInput.value || "0");
    query.set("logisticsCostPerOrder", elements.logisticsCostInput.value || "0");
    if (options.includeFullOrderLists) {
        query.set("includeFullOrderLists", "true");
    }
    return query;
}

function buildStreamerQuery() {
    const query = new URLSearchParams();
    query.set("store", elements.storeSelect.value || "all");
    if (elements.fromDateInput.value) query.set("fromDate", elements.fromDateInput.value);
    if (elements.toDateInput.value) query.set("toDate", elements.toDateInput.value);
    query.set("includeTikTokDiscount", elements.includeTikTokDiscountToggle.checked ? "true" : "false");
    query.set("includeBuyerShippingFee", "false");
    query.set("deductPlatformFee", elements.deductPlatformFeeToggle.checked ? "true" : "false");
    query.set("deductLogisticsCost", elements.deductLogisticsCostToggle.checked ? "true" : "false");
    query.set("platformFeeRate", elements.platformFeeRateInput.value || "0");
    query.set("logisticsCostPerOrder", elements.logisticsCostInput.value || "0");
    query.set("hiddenProcurementCostJpy", "0");
    return query;
}

async function fetchFullSummaryForExport() {
    setStatus("正在准备完整订单导出数据...", false);
    const response = await fetch(`/api/sales/summary?${buildQuery({ includeFullOrderLists: true }).toString()}`);
    if (!response.ok) {
        throw new Error("读取完整订单数据失败");
    }

    const summary = await response.json();
    setStatus("完整订单数据已准备好", false);
    return summary;
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
    const selected = elements.storeSelect.value || "all";
    elements.storeSelect.innerHTML = ['<option value="all">全部店铺</option>']
        .concat(state.stores.map((store) => `<option value="${escapeHtml(store.key)}">${escapeHtml(store.name)}</option>`))
        .join("");
    elements.storeSelect.value = state.stores.some((store) => store.key === selected) ? selected : "all";
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

function setActiveView(view) {
    state.activeView = view;
    elements.viewButtons.forEach((button) => button.classList.toggle("active", button.dataset.view === view));
    elements.viewPanels.forEach((panel) => panel.classList.toggle("active", panel.dataset.viewPanel === view));
    if (state.summary && state.derived) {
        renderActiveView();
    }
}

function renderSummary() {
    const summary = state.summary;
    const derived = state.derived;
    if (!summary || !derived) {
        return;
    }

    elements.generatedAtChip.textContent = `最近更新 ${formatDateTime(summary.generatedAtUtc)}`;
    elements.dailyChartMeta.textContent = `按实际支付日期统计，共 ${summary.daily.length} 个日期点`;
    elements.monthlyChartMeta.textContent = `当前返回 ${summary.monthly.length} 个自然月`;
    renderActiveView();
}

function renderActiveView() {
    const summary = state.summary;
    const derived = state.derived;
    if (!summary || !derived) {
        return;
    }

    switch (state.activeView) {
        case "overview":
            renderOverviewView(summary, derived);
            break;
        case "rhythm":
            renderRhythmView(summary, derived);
            break;
        case "commerce":
            renderCommerceView(summary);
            break;
        case "buyers":
            renderBuyersView(summary, derived);
            break;
        case "products":
            renderProductsView(summary, derived);
            break;
        case "reconciliation":
            renderReconciliation(summary, derived);
            break;
        case "risk":
            renderRisk(summary);
            break;
        default:
            renderOverviewView(summary, derived);
            break;
    }
}

function renderOverviewView(summary, derived) {
    renderOverviewCards(summary, derived);
    renderInsightCards(summary);
    renderTrendChart(summary.daily, elements.dailyTrendChart);
    renderColumnChart(summary.monthly, elements.monthlyBarChart, "month", summary.currency);
    renderWeekdayBreakdown(derived.weekdayRows, summary.currency);
    renderHourlyHeatmap(summary.hourly, summary.currency);
}

function renderRhythmView(summary, derived) {
    renderTable(elements.dailyTableBody, summary.daily, (item) => `
        <tr>
            <td>${escapeHtml(item.date)}</td>
            <td>${item.orderCount}</td>
            <td>${formatMoney(item.paidAmount, summary.currency)}</td>
            <td>${formatMoney(item.tikTokDiscountAmount, summary.currency)}</td>
            <td>${formatMoney(item.grossWithDiscount, summary.currency)}</td>
        </tr>`);

    renderTable(elements.monthlyTableBody, summary.monthly, (item) => `
        <tr>
            <td>${escapeHtml(item.month)}</td>
            <td>${item.orderCount}</td>
            <td>${formatMoney(item.paidAmount, summary.currency)}</td>
            <td>${formatMoney(item.tikTokDiscountAmount, summary.currency)}</td>
            <td>${formatMoney(item.grossWithDiscount, summary.currency)}</td>
        </tr>`);

    renderBucketBreakdown(elements.orderBucketChart, derived.orderBuckets, summary.currency);
    renderHandleSourceBreakdown(elements.handleSourceBreakdown, derived.handleSources, summary.currency);
}

function renderCommerceView(summary) {
    elements.compassScore.textContent = Number(summary.businessCompass.overallScore || 0).toFixed(1);
    renderCompass(summary.businessCompass);
    renderFunnel(summary.funnel);

    renderDonutPanel(elements.storeShareChart, summary.storeBreakdown, {
        title: "店铺销售占比",
        value: (item) => item.grossWithDiscount,
        label: (item) => item.storeName,
        currency: summary.currency
    });
    renderBarBreakdown(
        elements.storeBreakdown,
        summary.storeBreakdown,
        summary.currency,
        (item) => item.storeName,
        (item) => item.grossWithDiscount,
        (item) => `${item.orderCount} 单 · 客单价 ${formatMoney(item.averageOrderValue, summary.currency)}`
    );

    renderDonutPanel(elements.paymentShareChart, summary.paymentBreakdown, {
        title: "支付方式占比",
        value: (item) => item.grossWithDiscount,
        label: (item) => item.paymentMethod,
        currency: summary.currency
    });
    renderBarBreakdown(
        elements.paymentBreakdown,
        summary.paymentBreakdown,
        summary.currency,
        (item) => item.paymentMethod,
        (item) => item.grossWithDiscount,
        (item) => `${item.orderCount} 单 · 占比 ${formatPercent(item.paidAmountShareRate)}`
    );

    renderStatusList(summary.statusBreakdown, summary.currency);
    renderBarBreakdown(
        elements.hourlyBreakdown,
        summary.hourly,
        summary.currency,
        (item) => item.hourLabel,
        (item) => item.paidAmount,
        (item) => `${item.orderCount} 单`
    );
}

function renderBuyersView(summary, derived) {
    renderRankList(
        elements.buyerBreakdown,
        summary.paidBuyerRanking,
        summary.currency,
        (item) => item.buyerLabel,
        (item) => `${item.orderCount} 单 · 涉及 ${item.storeCount} 个店铺`
    );
    renderBuyerSegments(elements.buyerSegmentBreakdown, derived.buyerSegments, summary.currency);
    renderOrderList(elements.topOrders, summary.topOrders, summary.currency, false);
}

function renderProductsView(summary, derived) {
    renderRankList(
        elements.productBreakdown,
        summary.topProducts,
        summary.currency,
        (item) => item.displayName,
        (item) => `${item.orderCount} 单 · ${item.quantity || item.itemLineCount} 件 · Product ${item.productId || "-"}`
    );
    renderProductIdSuggestions(summary.productIdBreakdown || []);
    renderSelectedProductChips(summary.selectedProductIds || []);
    renderProductIdBreakdown(summary.productIdBreakdown || [], summary.selectedProductIds || [], summary.currency);
    renderSelectedProductDetails(summary.productIdBreakdown || [], summary.selectedProductIds || [], summary.currency);
    renderProductMonthQuickSummary(summary);
    renderRankList(
        elements.linkAttributionBreakdown,
        summary.linkAttributionBreakdown || [],
        summary.currency,
        (item) => item.label,
        (item) => `${item.orderCount} 单 • ${item.uniqueBuyerCount} 买家${item.linkUrl ? ` • ${item.linkUrl}` : ""}`
    );
    renderOrderList(elements.discountOrderList, derived.discountOrders, summary.currency, true);
}

function buildDerived(summary) {
    const derivedMetrics = summary.derivedMetrics || {};
    return {
        weekdayRows: deriveWeekdayRows(summary.daily || []),
        orderBuckets: derivedMetrics.orderBuckets || deriveOrderBuckets(summary.includedOrders || []),
        handleSources: derivedMetrics.handleSources || deriveHandleSources(summary.includedOrders || []),
        buyerSegments: derivedMetrics.buyerSegments || deriveBuyerSegments(summary.includedOrders || []),
        settlementRows: derivedMetrics.settlementRows || deriveSettlementRows(summary.includedOrders || []),
        discountOrders: derivedMetrics.discountOrders || [...(summary.includedOrders || [])]
            .filter((item) => Number(item.tikTokDiscountAmount || 0) > 0)
            .sort((left, right) => Number(right.tikTokDiscountAmount || 0) - Number(left.tikTokDiscountAmount || 0))
            .slice(0, 12),
        bestWeekday: deriveWeekdayRows(summary.daily || [])
            .slice()
            .sort((left, right) => right.grossWithDiscount - left.grossWithDiscount)[0] || null
    };
}

function normalizeProductIdInput(value) {
    return (value || "")
        .split(/[\s,\n\r\t]+/)
        .map((item) => item.trim())
        .filter(Boolean)
        .filter((item, index, array) => array.findIndex((candidate) => candidate.toLowerCase() === item.toLowerCase()) === index);
}

async function runMonthlyProductLookup() {
    if (!elements.productMonthQuickInput || !elements.productIdFilterInput) {
        return;
    }

    const ids = normalizeProductIdInput(elements.productMonthQuickInput.value || "");
    if (ids.length === 0) {
        setStatus("请输入一个 Product ID", true);
        elements.productMonthQuickInput.focus();
        return;
    }

    if (ids.length > 1) {
        setStatus("本月快查一次只支持一个 Product ID", true);
        elements.productMonthQuickInput.focus();
        return;
    }

    const productId = ids[0];
    elements.productMonthQuickInput.value = productId;
    elements.productIdFilterInput.value = productId;
    setDatePreset("month");
    setActiveView("products");
    await loadSummary();
}

function clearMonthlyProductLookup() {
    if (elements.productMonthQuickInput) {
        elements.productMonthQuickInput.value = "";
    }

    if (elements.productIdFilterInput) {
        elements.productIdFilterInput.value = "";
    }
}

function renderProductIdSuggestions(rows) {
    if (!elements.productIdSuggestions) {
        return;
    }

    const selected = normalizeProductIdInput((elements.productIdFilterInput?.value || "").trim());
    if (selected.length === 0 && Array.isArray(rows) && rows.length > 0 && elements.productIdFilterInput && state.summary?.selectedProductIds?.length) {
        elements.productIdFilterInput.value = state.summary.selectedProductIds.join(", ");
    }

    elements.productIdSuggestions.innerHTML = (rows || [])
        .slice(0, 200)
        .map((item) => `<option value="${escapeHtml(item.productId)}">${escapeHtml(item.displayName || item.productName || item.productId)}</option>`)
        .join("");
}

function renderSelectedProductChips(selectedProductIds) {
    if (!elements.selectedProductChips || !elements.productIdFilterInput) {
        return;
    }

    const ids = normalizeProductIdInput((selectedProductIds || []).join(","));
    elements.productIdFilterInput.value = ids.join(", ");

    if (!ids.length) {
        elements.selectedProductChips.innerHTML = '<span class="muted-pill">未选择 Product ID，下面显示当前区间全部 Product ID 汇总。</span>';
        return;
    }

    elements.selectedProductChips.innerHTML = ids
        .map((productId) => `
            <button type="button" class="chip-button" data-remove-product-id="${escapeHtml(productId)}">
                <span>${escapeHtml(productId)}</span>
                <strong>×</strong>
            </button>`)
        .join("");

    elements.selectedProductChips.querySelectorAll('[data-remove-product-id]').forEach((button) => {
        button.addEventListener('click', async () => {
            const nextIds = ids.filter((item) => item !== button.dataset.removeProductId);
            elements.productIdFilterInput.value = nextIds.join(', ');
            await loadSummary();
        });
    });
}

function renderProductIdBreakdown(rows, selectedProductIds, currency) {
    if (!elements.productIdBreakdown) {
        return;
    }

    if (!rows.length) {
        elements.productIdBreakdown.innerHTML = '<div class="empty-panel">当前区间没有可聚合的 Product ID 数据</div>';
        return;
    }

    const selected = new Set(normalizeProductIdInput((selectedProductIds || []).join(',')));
    const visibleRows = selected.size === 0 ? rows.slice(0, 60) : rows.filter((item) => selected.has(item.productId));

    elements.productIdBreakdown.innerHTML = visibleRows.map((item) => `
        <article class="product-id-card ${selected.has(item.productId) ? 'selected' : ''}">
            <div class="product-id-head">
                <div>
                    <span class="product-id-tag">Product ID</span>
                    <strong>${escapeHtml(item.productId)}</strong>
                    <p>${escapeHtml(item.productName || item.displayName || '-')}</p>
                </div>
                <div class="product-id-amount">${formatMoney(item.grossWithDiscount, currency)}</div>
            </div>
            <div class="product-id-grid">
                <div><span>订单数</span><strong>${item.orderCount}</strong></div>
                <div><span>件数</span><strong>${item.quantity || 0}</strong></div>
                <div><span>SKU 数</span><strong>${item.skuCount || 0}</strong></div>
                <div><span>店铺数</span><strong>${item.storeCount || 0}</strong></div>
                <div><span>销售额</span><strong>${formatMoney(item.salesAmount, currency)}</strong></div>
                <div><span>TikTok 折扣</span><strong>${formatMoney(item.tikTokDiscountAmount, currency)}</strong></div>
            </div>
            <div class="product-id-actions">
                <button type="button" class="secondary-button" data-pick-product-id="${escapeHtml(item.productId)}">只看这个 Product ID</button>
            </div>
        </article>`).join('');

    elements.productIdBreakdown.querySelectorAll('[data-pick-product-id]').forEach((button) => {
        button.addEventListener('click', async () => {
            const current = new Set(normalizeProductIdInput(elements.productIdFilterInput.value || ''));
            current.add(button.dataset.pickProductId || '');
            elements.productIdFilterInput.value = Array.from(current).join(', ');
            await loadSummary();
        });
    });
}

function renderSelectedProductDetails(rows, selectedProductIds, currency) {
    if (!elements.selectedProductDetails) {
        return;
    }

    const selected = new Set(normalizeProductIdInput((selectedProductIds || []).join(',')));
    if (!selected.size) {
        elements.selectedProductDetails.innerHTML = '<div class="empty-panel">输入 Product ID 后，这里会显示该产品在一店、二店的销售拆分和 SKU 汇总。</div>';
        return;
    }

    const selectedRows = rows.filter((item) => selected.has(item.productId));
    if (!selectedRows.length) {
        elements.selectedProductDetails.innerHTML = '<div class="empty-panel">当前筛选区间没有匹配到选中的 Product ID。</div>';
        return;
    }

    elements.selectedProductDetails.innerHTML = selectedRows.map((item) => `
        <article class="product-detail-card">
            <div class="product-id-head">
                <div>
                    <span class="product-id-tag">Product ID</span>
                    <strong>${escapeHtml(item.productId)}</strong>
                    <p>${escapeHtml(item.productName || item.displayName || '-')}</p>
                </div>
                <div class="product-id-amount">${formatMoney(item.grossWithDiscount, currency)}</div>
            </div>
            <div class="product-detail-section">
                <h3>店铺拆分</h3>
                <div class="mini-table">
                    ${(item.storeBreakdown || []).map((store) => `
                        <div class="mini-row">
                            <span>${escapeHtml(store.storeName)}</span>
                            <span>${store.orderCount} 单 / ${store.quantity || 0} 件</span>
                            <strong>${formatMoney(store.grossWithDiscount, currency)}</strong>
                        </div>`).join('') || '<div class="empty-panel compact">暂无店铺拆分</div>'}
                </div>
            </div>
            <div class="product-detail-section">
                <h3>SKU 汇总</h3>
                <div class="mini-table">
                    ${(item.skuBreakdown || []).map((sku) => `
                        <div class="mini-row sku-row">
                            <span>${escapeHtml(sku.displayName || sku.skuName || sku.skuId || '-')}</span>
                            <span>${sku.orderCount} 单 / ${sku.quantity || 0} 件</span>
                            <strong>${formatMoney(sku.grossWithDiscount, currency)}</strong>
                        </div>`).join('') || '<div class="empty-panel compact">暂无 SKU 汇总</div>'}
                </div>
            </div>
        </article>`).join('');
}

function renderProductMonthQuickSummary(summary) {
    if (!elements.productMonthQuickSummary) {
        return;
    }

    const quickIds = normalizeProductIdInput(elements.productMonthQuickInput?.value || "");
    const selectedIds = normalizeProductIdInput((summary?.selectedProductIds || []).join(","));
    const productId = quickIds[0] || (selectedIds.length === 1 ? selectedIds[0] : "");

    if (!productId) {
        elements.productMonthQuickSummary.innerHTML = '<div class="empty-panel">输入一个 Product ID 后，这里会直接显示它本月卖了多少。</div>';
        return;
    }

    if (elements.productMonthQuickInput) {
        elements.productMonthQuickInput.value = productId;
    }

    const selectedRow = (summary?.productIdBreakdown || []).find((item) => (item.productId || "").toLowerCase() === productId.toLowerCase());
    const [fromLabel, toLabel] = currentRangeLabels();
    if (!selectedRow) {
        elements.productMonthQuickSummary.innerHTML = `
            <article class="product-month-card empty">
                <div>
                    <span class="product-id-tag">本月快查</span>
                    <strong>${escapeHtml(productId)}</strong>
                    <p>${escapeHtml(summary?.storeName || "全部店铺")} · ${escapeHtml(fromLabel)} 至 ${escapeHtml(toLabel)}</p>
                </div>
                <div class="empty-panel compact">当前本月范围没有查到这个 Product ID 的成交数据。</div>
            </article>`;
        return;
    }

    elements.productMonthQuickSummary.innerHTML = `
        <article class="product-month-card">
            <div class="product-id-head">
                <div>
                    <span class="product-id-tag">本月快查</span>
                    <strong>${escapeHtml(selectedRow.productId)}</strong>
                    <p>${escapeHtml(selectedRow.productName || selectedRow.displayName || "-")}</p>
                </div>
                <div class="product-id-amount">${formatMoney(selectedRow.grossWithDiscount, summary.currency)}</div>
            </div>
            <div class="product-month-meta">
                <span>店铺：${escapeHtml(summary.storeName || "全部店铺")}</span>
                <span>范围：${escapeHtml(fromLabel)} 至 ${escapeHtml(toLabel)}</span>
            </div>
            <div class="product-month-grid">
                <div><span>本月订单数</span><strong>${selectedRow.orderCount}</strong></div>
                <div><span>本月件数</span><strong>${selectedRow.quantity || 0}</strong></div>
                <div><span>本月 SKU 数</span><strong>${selectedRow.skuCount || 0}</strong></div>
                <div><span>本月店铺数</span><strong>${selectedRow.storeCount || 0}</strong></div>
                <div><span>本月销售额</span><strong>${formatMoney(selectedRow.salesAmount, summary.currency)}</strong></div>
                <div><span>本月 TikTok 折扣</span><strong>${formatMoney(selectedRow.tikTokDiscountAmount, summary.currency)}</strong></div>
                <div><span>本月成交额</span><strong>${formatMoney(selectedRow.grossWithDiscount, summary.currency)}</strong></div>
            </div>
            <div class="mini-table">
                ${(selectedRow.storeBreakdown || []).map((store) => `
                    <div class="mini-row">
                        <span>${escapeHtml(store.storeName)}</span>
                        <span>${store.orderCount} 单 / ${store.quantity || 0} 件</span>
                        <strong>${formatMoney(store.grossWithDiscount, summary.currency)}</strong>
                    </div>`).join("") || '<div class="empty-panel compact">暂无店铺拆分</div>'}
            </div>
        </article>`;
}
function renderLinkRules() {
    if (!elements.linkRuleList) {
        return;
    }

    if (!state.linkRules.length) {
        state.linkRules = [createEmptyLinkRule()];
    }

    elements.linkRuleList.innerHTML = state.linkRules.map((rule, index) => `
        <article class="link-rule-card" data-link-rule-row="${escapeHtml(rule.id || String(index))}">
            <label class="toggle-card compact-rule">
                <input type="checkbox" class="link-rule-enabled" ${rule.enabled !== false ? "checked" : ""} />
                <span>启用这条规则</span>
                <small>关闭后该主播链接不会参与归因。</small>
            </label>
            <div class="link-rule-grid">
                <label class="field compact">
                    <span>主播 / 链接标签</span>
                    <input type="text" class="link-rule-label" value="${escapeHtml(rule.label || "")}" placeholder="例如：主播 A / 美甲直播间" />
                </label>
                <label class="field compact">
                    <span>商品链接</span>
                    <input type="text" class="link-rule-url" value="${escapeHtml(rule.linkUrl || "")}" placeholder="粘贴 TikTok 商品链接" />
                </label>
                <label class="field compact">
                    <span>店铺 Key</span>
                    <input type="text" class="link-rule-stores" value="${escapeHtml((rule.storeKeys || []).join(", "))}" placeholder="all 或 store1, store2" />
                </label>
                <label class="field compact">
                    <span>Product ID</span>
                    <input type="text" class="link-rule-product-ids" value="${escapeHtml((rule.productIds || []).join(", "))}" placeholder="可留空，多个逗号分隔" />
                </label>
                <label class="field compact">
                    <span>SKU ID</span>
                    <input type="text" class="link-rule-sku-ids" value="${escapeHtml((rule.skuIds || []).join(", "))}" placeholder="可留空，多个逗号分隔" />
                </label>
                <label class="field compact">
                    <span>关键词</span>
                    <input type="text" class="link-rule-keywords" value="${escapeHtml((rule.productNameKeywords || []).join(", "))}" placeholder="商品名关键词，多个逗号分隔" />
                </label>
            </div>
            <div class="link-rule-actions">
                <button type="button" class="ghost-button link-rule-remove" data-link-rule-remove="${escapeHtml(rule.id || String(index))}">删除规则</button>
            </div>
        </article>`).join("");

    elements.linkRuleList.querySelectorAll("[data-link-rule-remove]").forEach((button) => {
        button.addEventListener("click", () => {
            const id = button.dataset.linkRuleRemove;
            state.linkRules = state.linkRules.filter((rule) => (rule.id || "") !== id);
            if (state.linkRules.length === 0) {
                state.linkRules = [createEmptyLinkRule()];
            }
            renderLinkRules();
        });
    });
}

function readLinkRulesFromEditor() {
    if (!elements.linkRuleList) {
        return state.linkRules;
    }

    const rows = Array.from(elements.linkRuleList.querySelectorAll("[data-link-rule-row]"));
    return rows.map((row, index) => ({
        id: row.dataset.linkRuleRow || `rule-${index + 1}`,
        label: row.querySelector(".link-rule-label")?.value?.trim() || "",
        linkUrl: row.querySelector(".link-rule-url")?.value?.trim() || "",
        enabled: Boolean(row.querySelector(".link-rule-enabled")?.checked),
        storeKeys: splitRuleValues(row.querySelector(".link-rule-stores")?.value || ""),
        productIds: splitRuleValues(row.querySelector(".link-rule-product-ids")?.value || ""),
        skuIds: splitRuleValues(row.querySelector(".link-rule-sku-ids")?.value || ""),
        productNameKeywords: splitRuleValues(row.querySelector(".link-rule-keywords")?.value || "")
    })).filter((rule) => rule.label || rule.linkUrl || rule.productIds.length || rule.skuIds.length || rule.productNameKeywords.length);
}

function createEmptyLinkRule() {
    return {
        id: `rule-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`,
        label: "",
        linkUrl: "",
        enabled: true,
        storeKeys: [],
        productIds: [],
        skuIds: [],
        productNameKeywords: []
    };
}

function splitRuleValues(value) {
    return String(value || "")
        .split(",")
        .map((item) => item.trim())
        .filter(Boolean);
}

function deriveWeekdayRows(dailyRows) {
    const weekdayOrder = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];
    const weekdayMap = new Map(weekdayOrder.map((label) => [label, { label, orderCount: 0, paidAmount: 0, tikTokDiscountAmount: 0, grossWithDiscount: 0 }]));

    dailyRows.forEach((row) => {
        const date = new Date(`${row.date}T00:00:00`);
        if (Number.isNaN(date.getTime())) {
            return;
        }

        const label = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"][date.getDay()];
        const bucket = weekdayMap.get(label);
        if (!bucket) {
            return;
        }

        bucket.orderCount += Number(row.orderCount || 0);
        bucket.paidAmount += Number(row.paidAmount || 0);
        bucket.tikTokDiscountAmount += Number(row.tikTokDiscountAmount || 0);
        bucket.grossWithDiscount += Number(row.grossWithDiscount || 0);
    });

    return weekdayOrder.map((label) => weekdayMap.get(label));
}

function deriveOrderBuckets(orders) {
    const buckets = [
        { label: "0-999", min: 0, max: 999.99, orderCount: 0, grossWithDiscount: 0 },
        { label: "1000-2999", min: 1000, max: 2999.99, orderCount: 0, grossWithDiscount: 0 },
        { label: "3000-4999", min: 3000, max: 4999.99, orderCount: 0, grossWithDiscount: 0 },
        { label: "5000-9999", min: 5000, max: 9999.99, orderCount: 0, grossWithDiscount: 0 },
        { label: "10000+", min: 10000, max: Number.POSITIVE_INFINITY, orderCount: 0, grossWithDiscount: 0 }
    ];

    orders.forEach((order) => {
        const amount = Number(order.grossWithDiscount || 0);
        const bucket = buckets.find((candidate) => amount >= candidate.min && amount <= candidate.max);
        if (!bucket) {
            return;
        }

        bucket.orderCount += 1;
        bucket.grossWithDiscount += amount;
    });

    return buckets;
}

function deriveHandleSources(orders) {
    const labels = {
        local_bridge: "本地桥接",
        api: "API 返回",
        api_only: "仅订单接口",
        unknown: "未识别"
    };

    const map = new Map();
    orders.forEach((order) => {
        const key = order.buyerHandleSource || "unknown";
        if (!map.has(key)) {
            map.set(key, { label: labels[key] || key, orderCount: 0, grossWithDiscount: 0 });
        }

        const bucket = map.get(key);
        bucket.orderCount += 1;
        bucket.grossWithDiscount += Number(order.grossWithDiscount || 0);
    });

    return [...map.values()].sort((left, right) => right.orderCount - left.orderCount);
}

function deriveBuyerSegments(orders) {
    const buyerMap = new Map();
    orders.forEach((order) => {
        const key = order.buyerHandle;
        if (!key) {
            return;
        }

        if (!buyerMap.has(key)) {
            buyerMap.set(key, { orderCount: 0, grossWithDiscount: 0 });
        }

        const buyer = buyerMap.get(key);
        buyer.orderCount += 1;
        buyer.grossWithDiscount += Number(order.grossWithDiscount || 0);
    });

    const segments = [
        { label: "1 单新客", test: (count) => count === 1, buyerCount: 0, orderCount: 0, grossWithDiscount: 0 },
        { label: "2-3 单轻复购", test: (count) => count >= 2 && count <= 3, buyerCount: 0, orderCount: 0, grossWithDiscount: 0 },
        { label: "4-6 单高频客", test: (count) => count >= 4 && count <= 6, buyerCount: 0, orderCount: 0, grossWithDiscount: 0 },
        { label: "7 单以上核心客", test: (count) => count >= 7, buyerCount: 0, orderCount: 0, grossWithDiscount: 0 }
    ];

    for (const buyer of buyerMap.values()) {
        const segment = segments.find((candidate) => candidate.test(buyer.orderCount));
        if (!segment) {
            continue;
        }

        segment.buyerCount += 1;
        segment.orderCount += buyer.orderCount;
        segment.grossWithDiscount += buyer.grossWithDiscount;
    }

    return segments;
}

function deriveSettlementRows(orders) {
    const settled = orders.filter((order) => String(order.settlementState || "").includes("已完结"));
    const pending = orders.filter((order) => !String(order.settlementState || "").includes("已完结"));
    return [
        {
            label: "已回款估算",
            amount: settled.reduce((sum, order) => sum + Number(order.estimatedReceivableAmount || 0), 0),
            orderCount: settled.length
        },
        {
            label: "待回款估算",
            amount: pending.reduce((sum, order) => sum + Number(order.estimatedReceivableAmount || 0), 0),
            orderCount: pending.length
        }
    ];
}

function renderOverviewCards(summary, derived) {
    const overview = summary.overview;
    const reconciliation = summary.reconciliation;
    const streamerTotals = state.streamerSummary?.totals || {};
    const selfOwned = state.streamerSummary?.selfOwned || {};
    const streamerLoaded = Boolean(state.streamerSummary);
    const logisticsPerOrder = Number(elements.logisticsCostInput.value || 0);
    const cards = [
        { title: "实际支付", value: formatMoney(reconciliation.basePaidAmount, summary.currency), note: `${overview.includedOrderCount} 单已支付订单`, tone: "accent" },
        { title: "折扣支付", value: formatMoney(reconciliation.tikTokDiscountAmount, summary.currency), note: "折扣回款归你自己，不计主播提成" },
        { title: "预估运费", value: formatMoney(reconciliation.estimatedShippingFeeAmount, summary.currency), note: `${reconciliation.estimatedShippingOrderCount || 0} 单 × ${formatMoney(logisticsPerOrder, summary.currency)}` },
        { title: "实际运费", value: formatMoney(reconciliation.actualShippingFeeAmount, summary.currency), note: `已算好运费 ${reconciliation.calculatedShippingOrderCount || 0} 单，补估 ${reconciliation.actualShippingFallbackOrderCount || 0} 单` },
        { title: "平台佣金", value: formatMoney(reconciliation.estimatedPlatformFeeAmount, summary.currency), note: summary.settings.deductPlatformFee ? "按费率已扣除" : "当前未扣除" },
        { title: "预估可回款", value: formatMoney(reconciliation.estimatedReceivableAfterEstimatedShippingAmount || 0, summary.currency), note: "实际支付 + 折扣 - 预估运费 - 平台佣金" },
        { title: "实际可回款", value: formatMoney(reconciliation.actualReceivableAfterActualShippingAmount || 0, summary.currency), note: "实际支付 + 折扣 - 实际运费 - 平台佣金" },
        { title: "主播提成", value: formatMoney(streamerTotals.salaryCommissionAmountJpy || 0, "JPY"), note: streamerLoaded ? "只按实际支付计算，不含折扣" : "主播汇总加载中…" },
        { title: "自营利润", value: formatMoney(selfOwned.profitAfterHiddenCostWithCalculatedShippingJpy ?? selfOwned.profitAfterHiddenCostJpy ?? 0, "JPY"), note: streamerLoaded ? "未分配给主播的链接利润，折扣归你自己" : "主播汇总加载中…" },
        { title: "已回款平均运费", value: formatMoney(reconciliation.settledAverageShippingFeeAmount || 0, summary.currency), note: `${reconciliation.completedOrderCount || 0} 单已回款订单平均` }
    ];

    elements.overviewCards.innerHTML = cards.map((card) => `
        <article class="overview-card ${card.tone || ""}">
            <span>${escapeHtml(card.title)}</span>
            <strong>${escapeHtml(card.value)}</strong>
            <small>${escapeHtml(card.note)}</small>
        </article>`).join("");
}

function renderInsightCards(summary) {
    const insightCards = [
        summary.insights.bestDay,
        summary.insights.bestHour,
        summary.insights.bestStore,
        summary.insights.bestPayment,
        summary.insights.bestBuyer,
        summary.insights.bestProduct
    ];

    elements.insightCards.innerHTML = insightCards.map((item) => `
        <article class="insight-card">
            <span>${escapeHtml(item.title)}</span>
            <strong>${escapeHtml(item.label || "-")}</strong>
            <small>${escapeHtml(item.value || "-")} · ${escapeHtml(item.note || "")}</small>
        </article>`).join("");
}

function renderTrendChart(items, target) {
    if (!items.length) {
        target.innerHTML = emptyChart("暂无趋势数据");
        return;
    }

    const width = 760;
    const height = 280;
    const left = 50;
    const right = 16;
    const top = 20;
    const bottom = 34;
    const chartWidth = width - left - right;
    const chartHeight = height - top - bottom;
    const max = Math.max(...items.flatMap((item) => [
        Number(item.paidAmount || 0),
        Number(item.tikTokDiscountAmount || 0),
        Number(item.grossWithDiscount || 0)
    ]), 1);
    const stepX = items.length === 1 ? 0 : chartWidth / (items.length - 1);

    const series = [
        { key: "paidAmount", color: "#0a7f6f", label: "实付" },
        { key: "tikTokDiscountAmount", color: "#d48f1f", label: "TikTok折扣" },
        { key: "grossWithDiscount", color: "#0f5d75", label: "成交额" }
    ];

    const paths = series.map((seriesItem) => {
        const points = items.map((item, index) => {
            const value = Number(item[seriesItem.key] || 0);
            return {
                x: left + stepX * index,
                y: top + chartHeight - (value / max) * chartHeight
            };
        });

        return { color: seriesItem.color, label: seriesItem.label, points };
    });

    target.innerHTML = `
        <div class="chart-legend">
            ${paths.map((item) => `<span><i style="background:${item.color}"></i>${escapeHtml(item.label)}</span>`).join("")}
        </div>
        <svg class="chart-svg" viewBox="0 0 ${width} ${height}" preserveAspectRatio="none">
            <line class="chart-grid" x1="${left}" y1="${top}" x2="${left}" y2="${top + chartHeight}" />
            <line class="chart-grid" x1="${left}" y1="${top + chartHeight}" x2="${width - right}" y2="${top + chartHeight}" />
            ${paths.map((item) => `<path class="chart-line" d="${toLinePath(item.points)}" style="stroke:${item.color}" />`).join("")}
            ${paths.map((item) => item.points.map((point) => `<circle class="chart-dot" cx="${point.x}" cy="${point.y}" r="3.5" style="fill:${item.color}" />`).join("")).join("")}
            ${items.map((item, index) => {
                const x = left + stepX * index;
                return `<text class="chart-label" x="${x}" y="${height - 10}" text-anchor="middle">${escapeHtml(shortDate(item.date))}</text>`;
            }).join("")}
        </svg>`;
}

function renderColumnChart(items, target, labelKey, currency) {
    if (!items.length) {
        target.innerHTML = emptyChart("暂无月度数据");
        return;
    }

    const max = Math.max(...items.map((item) => Number(item.grossWithDiscount || 0)), 1);
    target.innerHTML = `
        <div class="column-grid">
            ${items.map((item) => {
                const percent = (Number(item.grossWithDiscount || 0) / max) * 100;
                return `
                    <article class="column-card">
                        <div class="column-value">
                            <strong>${formatMoney(item.grossWithDiscount, currency)}</strong>
                            <span>${item.orderCount} 单</span>
                        </div>
                        <div class="column-track">
                            <span class="column-fill" style="height:${Math.max(percent, 6)}%"></span>
                        </div>
                        <div class="column-label">${escapeHtml(item[labelKey])}</div>
                    </article>`;
            }).join("")}
        </div>`;
}

function renderWeekdayBreakdown(rows, currency) {
    renderBarBreakdown(
        elements.weekdayBreakdown,
        rows.filter(Boolean),
        currency,
        (item) => item.label,
        (item) => item.grossWithDiscount,
        (item) => `${item.orderCount} 单 · 实付 ${formatMoney(item.paidAmount, currency)}`
    );
}

function renderHourlyHeatmap(rows, currency) {
    const values = new Array(24).fill(0).map((_, hour) => ({ hour, amount: 0, orderCount: 0 }));
    rows.forEach((row) => {
        const hour = Number.parseInt(String(row.hourLabel || "").split(":")[0], 10);
        if (Number.isNaN(hour) || hour < 0 || hour > 23) {
            return;
        }

        values[hour] = {
            hour,
            amount: Number(row.paidAmount || 0),
            orderCount: Number(row.orderCount || 0)
        };
    });

    const max = Math.max(...values.map((item) => item.amount), 1);
    elements.hourlyHeatmap.innerHTML = values.map((item) => {
        const intensity = item.amount === 0 ? 0.08 : Math.max(item.amount / max, 0.12);
        return `
            <div class="heatmap-cell" style="--cell-alpha:${intensity}">
                <span class="heatmap-label">${String(item.hour).padStart(2, "0")}:00</span>
                <strong class="heatmap-value">${item.orderCount}</strong>
                <small>${formatMoney(item.amount, currency)}</small>
            </div>`;
    }).join("");
}

function renderCompass(compass) {
    const axes = compass.axes || [];
    if (!axes.length) {
        elements.businessCompassChart.innerHTML = emptyChart("暂无罗盘数据");
        elements.compassAxisList.innerHTML = "";
        return;
    }

    const width = 380;
    const height = 380;
    const centerX = width / 2;
    const centerY = height / 2;
    const radius = 132;

    const points = axes.map((axis, index) => {
        const angle = (-Math.PI / 2) + (Math.PI * 2 * index / axes.length);
        const valueRadius = radius * (Number(axis.score || 0) / 100);
        return {
            axis,
            outerX: centerX + Math.cos(angle) * radius,
            outerY: centerY + Math.sin(angle) * radius,
            valueX: centerX + Math.cos(angle) * valueRadius,
            valueY: centerY + Math.sin(angle) * valueRadius
        };
    });

    elements.businessCompassChart.innerHTML = `
        <svg class="radar-svg" viewBox="0 0 ${width} ${height}" preserveAspectRatio="xMidYMid meet">
            <circle class="radar-grid" cx="${centerX}" cy="${centerY}" r="${radius}" />
            <circle class="radar-grid" cx="${centerX}" cy="${centerY}" r="${radius * 0.66}" />
            <circle class="radar-grid" cx="${centerX}" cy="${centerY}" r="${radius * 0.33}" />
            ${points.map((point) => `<line class="radar-axis" x1="${centerX}" y1="${centerY}" x2="${point.outerX}" y2="${point.outerY}" />`).join("")}
            <polygon class="radar-shape" points="${points.map((point) => `${point.valueX},${point.valueY}`).join(" ")}"></polygon>
            ${points.map((point) => `<text class="radar-label" x="${point.outerX}" y="${point.outerY}" text-anchor="middle">${escapeHtml(point.axis.label)}</text>`).join("")}
        </svg>`;

    elements.compassAxisList.innerHTML = axes.map((axis) => `
        <article class="axis-card">
            <div class="axis-head">
                <strong>${escapeHtml(axis.label)}</strong>
                <span>${Number(axis.score || 0).toFixed(1)}</span>
            </div>
            <div class="axis-track"><span class="axis-fill" style="width:${Number(axis.score || 0)}%"></span></div>
            <div class="axis-value">${escapeHtml(axis.valueLabel || "")}</div>
            <small>${escapeHtml(axis.description || "")}</small>
        </article>`).join("");
}

function renderFunnel(funnel) {
    const rows = [
        { label: "观察订单", value: funnel.observedOrderCount },
        { label: "计入销售", value: funnel.includedOrderCount },
        { label: "待揽收状态单", value: funnel.awaitingCollectionStatusCount },
        { label: "未支付排除", value: funnel.excludedUnpaidCount },
        { label: "取消排除", value: funnel.excludedCancelledCount },
        { label: "退款/关闭排除", value: funnel.excludedRefundedCount }
    ];

    const max = Math.max(...rows.map((row) => row.value), 1);
    elements.funnelCards.innerHTML = rows.map((row) => `
        <article class="funnel-card">
            <div class="funnel-head">
                <strong>${escapeHtml(row.label)}</strong>
                <span>${row.value} 单</span>
            </div>
            <div class="funnel-track"><span class="funnel-fill" style="width:${(row.value / max) * 100}%"></span></div>
        </article>`).join("");
}

function renderDonutPanel(target, rows, config) {
    if (!rows.length) {
        target.innerHTML = emptyChart("暂无结构数据");
        return;
    }

    const colors = ["#0a7f6f", "#0f5d75", "#d48f1f", "#9a5b2f", "#6f47a6", "#c05c83", "#56733c"];
    const total = rows.reduce((sum, item) => sum + Number(config.value(item) || 0), 0);
    let cursor = -90;

    const slices = rows.map((item, index) => {
        const value = Number(config.value(item) || 0);
        const percent = total === 0 ? 0 : value / total;
        const startAngle = cursor;
        const endAngle = cursor + percent * 360;
        cursor = endAngle;
        return {
            label: config.label(item),
            value,
            note: item.orderCount ? `${item.orderCount} 单` : "",
            color: colors[index % colors.length],
            path: describeArc(90, 90, 62, startAngle, endAngle)
        };
    });

    target.innerHTML = `
        <div class="donut-shell">
            <div class="donut-title">${escapeHtml(config.title)}</div>
            <svg class="donut-svg" viewBox="0 0 180 180" preserveAspectRatio="xMidYMid meet">
                <circle cx="90" cy="90" r="62" fill="none" stroke="rgba(111,96,85,0.12)" stroke-width="24"></circle>
                ${slices.map((slice) => `<path d="${slice.path}" fill="none" stroke="${slice.color}" stroke-width="24" stroke-linecap="round"></path>`).join("")}
                <text x="90" y="84" text-anchor="middle" class="donut-center-label">总额</text>
                <text x="90" y="108" text-anchor="middle" class="donut-center-value">${escapeHtml(formatMoney(total, config.currency))}</text>
            </svg>
            <div class="donut-legend">
                ${slices.map((slice) => `
                    <div class="legend-row">
                        <span class="legend-swatch" style="background:${slice.color}"></span>
                        <div class="legend-copy">
                            <strong>${escapeHtml(slice.label)}</strong>
                            <small>${escapeHtml(slice.note)}</small>
                        </div>
                        <span class="legend-value">${escapeHtml(formatMoney(slice.value, config.currency))}</span>
                    </div>`).join("")}
            </div>
        </div>`;
}

function renderBarBreakdown(target, rows, currency, labelFn, valueFn, noteFn) {
    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无数据</div>`;
        return;
    }

    const max = Math.max(...rows.map((item) => Number(valueFn(item) || 0)), 1);
    target.innerHTML = rows.map((item) => {
        const value = Number(valueFn(item) || 0);
        const percent = (value / max) * 100;
        return `
            <div class="bar-row">
                <div class="bar-track"><span class="bar-fill" style="width:${percent}%"></span></div>
                <div class="bar-content">
                    <div class="metric-meta">
                        <strong>${escapeHtml(labelFn(item))}</strong>
                        <span>${escapeHtml(noteFn(item))}</span>
                    </div>
                    <div class="metric-values">
                        <strong>${formatMoney(value, currency)}</strong>
                    </div>
                </div>
            </div>`;
    }).join("");
}

function renderBucketBreakdown(target, rows, currency) {
    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无金额带数据</div>`;
        return;
    }

    const max = Math.max(...rows.map((row) => row.orderCount), 1);
    target.innerHTML = rows.map((row) => `
        <div class="bar-row">
            <div class="bar-track"><span class="bar-fill" style="width:${(row.orderCount / max) * 100}%"></span></div>
            <div class="bar-content">
                <div class="metric-meta">
                    <strong>${escapeHtml(row.label)}</strong>
                    <span>${row.orderCount} 单</span>
                </div>
                <div class="metric-values">
                    <strong>${formatMoney(row.grossWithDiscount, currency)}</strong>
                </div>
            </div>
        </div>`).join("");
}

function renderHandleSourceBreakdown(target, rows, currency) {
    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无用户名来源数据</div>`;
        return;
    }

    const max = Math.max(...rows.map((row) => row.orderCount), 1);
    target.innerHTML = rows.map((row) => `
        <div class="bar-row">
            <div class="bar-track"><span class="bar-fill" style="width:${(row.orderCount / max) * 100}%"></span></div>
            <div class="bar-content">
                <div class="metric-meta">
                    <strong>${escapeHtml(row.label)}</strong>
                    <span>${row.orderCount} 单</span>
                </div>
                <div class="metric-values">
                    <strong>${formatMoney(row.grossWithDiscount, currency)}</strong>
                </div>
            </div>
        </div>`).join("");
}

function renderStatusList(rows, currency) {
    if (!rows.length) {
        elements.statusBreakdown.innerHTML = `<div class="empty-panel">暂无状态数据</div>`;
        return;
    }

    elements.statusBreakdown.innerHTML = rows.map((item) => `
        <article class="status-card">
            <span>${escapeHtml(formatStatus(item.status))}</span>
            <strong>${item.orderCount} 单</strong>
            <small>${escapeHtml(formatClassification(item.classification))} · ${formatMoney(item.paidAmount, currency)}</small>
        </article>`).join("");
}

function renderRankList(target, rows, currency, titleFn, noteFn) {
    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无排行数据</div>`;
        return;
    }

    target.innerHTML = rows.map((item, index) => `
        <article class="rank-row">
            <div class="rank-index">${index + 1}</div>
            <div class="rank-main">
                <strong>${escapeHtml(titleFn(item))}</strong>
                <p>${escapeHtml(noteFn(item))}</p>
            </div>
            <div class="rank-side"><strong>${formatMoney(item.grossWithDiscount, currency)}</strong></div>
        </article>`).join("");
}

function renderBuyerSegments(target, rows, currency) {
    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无客户分层数据</div>`;
        return;
    }

    const max = Math.max(...rows.map((row) => row.buyerCount), 1);
    target.innerHTML = rows.map((row) => `
        <div class="bar-row">
            <div class="bar-track"><span class="bar-fill" style="width:${(row.buyerCount / max) * 100}%"></span></div>
            <div class="bar-content">
                <div class="metric-meta">
                    <strong>${escapeHtml(row.label)}</strong>
                    <span>${row.buyerCount} 人 · ${row.orderCount} 单</span>
                </div>
                <div class="metric-values">
                    <strong>${formatMoney(row.grossWithDiscount, currency)}</strong>
                </div>
            </div>
        </div>`).join("");
}

function renderOrderList(target, rows, currency, detailed) {
    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无订单数据</div>`;
        return;
    }

    target.innerHTML = rows.map((item) => `
        <article class="order-card ${detailed && item.exclusionReason ? "excluded" : ""}">
            <div class="order-head">
                <div>
                    <span class="order-tag ${item.exclusionReason ? "excluded" : "muted"}">${escapeHtml(item.exclusionReason ? formatClassification(item.exclusionReason) : "计入销售")}</span>
                    <strong>#${escapeHtml(item.orderId)}</strong>
                </div>
                <div class="order-amount">${formatMoney(item.grossWithDiscount, currency)}</div>
            </div>
            <div class="order-body">
                <div class="order-line"><span>用户</span><strong>${escapeHtml(item.buyerHandle || item.buyerUserId || item.buyerEmail || "未识别")}</strong></div>
                <div class="order-line"><span>状态</span><strong>${escapeHtml(formatStatus(item.status))}</strong></div>
                <div class="order-line"><span>支付方式</span><strong>${escapeHtml(item.paymentMethod || "-")}</strong></div>
                <div class="order-line"><span>支付时间</span><strong>${escapeHtml(item.paidAtLocal || "-")}</strong></div>
                ${detailed ? `<div class="order-line wide"><span>商品摘要</span><strong>${escapeHtml(item.primaryProductName || "-")}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>回款状态</span><strong>${escapeHtml(item.settlementState || "-")}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>平台费</span><strong>${formatMoney(item.estimatedPlatformFeeAmount || 0, currency)}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>物流费</span><strong>${formatMoney(item.estimatedLogisticsCostAmount || 0, currency)}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>已算好运费</span><strong>${item.hasCalculatedShippingFee ? formatMoney(item.calculatedShippingFeeAmount || 0, currency) : "未出"}</strong></div>` : ""}
            </div>
        </article>`).join("");
}

function renderReminderList(target, rows, currency) {
    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无未支付提醒</div>`;
        return;
    }

    target.innerHTML = rows.map((item) => `
        <article class="order-card reminder">
            <div class="order-head">
                <div>
                    <span class="order-tag warning">未支付提醒</span>
                    <strong>#${escapeHtml(item.orderId)}</strong>
                </div>
                <div class="order-amount">${formatMoney(item.expectedAmount, currency)}</div>
            </div>
            <div class="order-body">
                <div class="order-line"><span>客户</span><strong>${escapeHtml(item.buyerLabel || item.buyerUserId || item.buyerEmail || "未识别")}</strong></div>
                <div class="order-line"><span>支付方式</span><strong>${escapeHtml(item.paymentMethod || "-")}</strong></div>
                <div class="order-line"><span>下单时间</span><strong>${escapeHtml(item.createdAtLocal || "-")}</strong></div>
                <div class="order-line"><span>已等待</span><strong>${Number(item.hoursOpen || 0).toFixed(1)} 小时</strong></div>
                <div class="order-line"><span>便利店支付</span><strong>${item.isConvenienceStorePayment ? "是" : "否"}</strong></div>
                <div class="order-line"><span>是否首单</span><strong>${item.isFirstObservedOrder ? "是" : "否"}</strong></div>
            </div>
        </article>`).join("");
}

function renderRiskList(target, rows, currency) {
    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无名单数据</div>`;
        return;
    }

    target.innerHTML = rows.map((item) => `
        <article class="risk-card ${String(item.reason || "").includes("取消") ? "blacklist" : "potential"}">
            <div class="risk-head">
                <strong>${escapeHtml(item.buyerLabel || item.buyerUserId || item.buyerEmail || "未识别")}</strong>
                <span>${formatMoney(item.triggerAmount || 0, currency)}</span>
            </div>
            <p>${escapeHtml(item.reason || "-")}</p>
            <div class="risk-meta">
                <span>首单：#${escapeHtml(item.firstOrderId || "-")}</span>
                <span>触发单：#${escapeHtml(item.triggerOrderId || "-")}</span>
                <span>状态：${escapeHtml(item.triggerStatus || "-")}</span>
            </div>
        </article>`).join("");
}

function renderReconciliation(summary, derived) {
    const data = summary.reconciliation;
    const cards = [
        { title: "平台已算好运费", value: formatMoney(data.calculatedShippingFeeAmount || 0, summary.currency), note: `${data.calculatedShippingOrderCount || 0} 单已出真实运费` },
        { title: "补估运费", value: formatMoney((data.actualShippingFeeAmount || 0) - (data.calculatedShippingFeeAmount || 0), summary.currency), note: `${data.actualShippingFallbackOrderCount || 0} 单按单票物流成本补估` },
        { title: "已回款平均运费", value: formatMoney(data.settledAverageShippingFeeAmount || 0, summary.currency), note: `${data.completedOrderCount || 0} 单已回款订单平均` },
        { title: "已回款(实际运费版)", value: formatMoney(data.actualSettledReceivableAfterActualShippingAmount || 0, summary.currency), note: "实际支付 + 折扣 - 实际运费 - 平台佣金" },
        { title: "待回款(实际运费版)", value: formatMoney(data.actualPendingReceivableAfterActualShippingAmount || 0, summary.currency), note: `${data.pendingSettlementOrderCount || 0} 单待回款` },
        { title: "预估可回款(估算物流版)", value: formatMoney(data.estimatedReceivableAfterEstimatedShippingAmount || 0, summary.currency), note: "实际支付 + 折扣 - 预估运费 - 平台佣金" },
        { title: "结算完成率", value: formatPercent(data.settlementCompletionRate), note: "已回款订单 / 全部有效订单" }
    ];

    elements.reconciliationCards.innerHTML = cards.map((card) => `
        <article class="overview-card">
            <span>${escapeHtml(card.title)}</span>
            <strong>${escapeHtml(card.value)}</strong>
            <small>${escapeHtml(card.note)}</small>
        </article>`).join("");

    renderDonutPanel(elements.settlementBreakdown, derived.settlementRows, {
        title: "回款结构",
        value: (item) => item.amount,
        label: (item) => item.label,
        currency: summary.currency
    });

    elements.reconciliationNote.textContent = data.note || "";
    elements.includedCountLabel.textContent = summary.includedOrdersTruncated
        ? `${summary.includedOrders.length} / ${summary.includedOrderTotalCount} 单`
        : `${summary.includedOrderTotalCount || summary.includedOrders.length} 单`;
    renderOrderList(elements.includedOrders, summary.includedOrders, summary.currency, true);
}

function renderRisk(summary) {
    const riskCards = [
        { title: "未支付提醒", value: `${summary.unpaidReminders.length} 单`, note: "重点提醒便利店支付未完成客户", tone: "warning" },
        { title: "潜在客户", value: `${summary.potentialCustomers.length} 人`, note: "首单便利店未支付", tone: "warning" },
        { title: "黑名单候选", value: `${summary.blacklistCandidates.length} 人`, note: "首单下单后取消", tone: "danger" },
        { title: "排除订单", value: `${summary.excludedOrders.length} 单`, note: "未支付、取消、退款/关闭", tone: "danger" }
    ];

    elements.riskCards.innerHTML = riskCards.map((card) => `
        <article class="overview-card ${card.tone || ""}">
            <span>${escapeHtml(card.title)}</span>
            <strong>${escapeHtml(card.value)}</strong>
            <small>${escapeHtml(card.note)}</small>
        </article>`).join("");

    elements.excludedCountLabel.textContent = summary.excludedOrdersTruncated
        ? `${summary.excludedOrders.length} / ${summary.excludedOrderTotalCount} 单`
        : `${summary.excludedOrderTotalCount || summary.excludedOrders.length} 单`;
    renderReminderList(elements.unpaidReminderList, summary.unpaidReminders, summary.currency);
    renderRiskList(elements.potentialCustomerList, summary.potentialCustomers, summary.currency);
    renderRiskList(elements.blacklistList, summary.blacklistCandidates, summary.currency);
    renderOrderList(elements.excludedOrders, summary.excludedOrders, summary.currency, true);
}

function renderTable(target, rows, rowRenderer) {
    if (!rows.length) {
        target.innerHTML = elements.emptyRowTemplate.innerHTML;
        return;
    }

    target.innerHTML = rows.map(rowRenderer).join("");
}

async function copyText(text, successMessage) {
    await navigator.clipboard.writeText(text);
    setStatus(successMessage, false);
}

function buildSummaryClipboard(summary) {
    return [
        `店铺：${summary.storeName}`,
        `区间：${summary.fromUtc.slice(0, 10)} ~ ${summary.toUtc.slice(0, 10)}`,
        `有效订单数：${summary.overview.includedOrderCount}`,
        `实付金额：${formatMoney(summary.overview.includedPaidAmount, summary.currency)}`,
        `TikTok 折扣：${formatMoney(summary.overview.includedTikTokDiscountAmount, summary.currency)}`,
        `成交额：${formatMoney(summary.overview.includedGrossWithDiscount, summary.currency)}`,
        `预估可回款：${formatMoney(summary.reconciliation.estimatedReceivableAfterEstimatedShippingAmount || 0, summary.currency)}`,
        `实际可回款：${formatMoney(summary.reconciliation.actualReceivableAfterActualShippingAmount || 0, summary.currency)}`,
        `未支付提醒：${summary.unpaidReminders.length}`,
        `黑名单候选：${summary.blacklistCandidates.length}`
    ].join("\n");
}

function buildOrderIdsClipboard(summary) {
    return (summary.includedOrders || []).map((item) => item.orderId).join("\n");
}

function buildOrderAmountsClipboard(summary) {
    const header = ["订单号", "用户名", "实付金额", "TikTok折扣", "成交额"];
    const rows = (summary.includedOrders || []).map((item) => [
        item.orderId,
        item.buyerHandle || item.buyerUserId || item.buyerEmail || "",
        String(item.paidAmount || 0),
        String(item.tikTokDiscountAmount || 0),
        String(item.grossWithDiscount || 0)
    ]);
    return [header, ...rows].map((row) => row.join("\t")).join("\n");
}

function buildCsv(summary) {
    const header = ["订单号", "用户名", "BuyerUserId", "订单状态", "支付方式", "支付时间", "实付金额", "TikTok折扣", "成交额"];
    const rows = (summary.includedOrders || []).map((item) => [
        item.orderId,
        item.buyerHandle || "",
        item.buyerUserId || "",
        item.status || "",
        item.paymentMethod || "",
        item.paidAtLocal || "",
        item.paidAmount || 0,
        item.tikTokDiscountAmount || 0,
        item.grossWithDiscount || 0
    ]);

    return [header, ...rows].map((row) => row.map(csvEscape).join(",")).join("\n");
}

function setStatus(message, isError) {
    elements.statusChip.textContent = message;
    elements.statusChip.classList.toggle("status-error", Boolean(isError));
}

function updatePresetButtons() {
    elements.presetButtons.forEach((button) => {
        button.classList.toggle("active", button.dataset.preset === state.activePreset);
    });
}

function currentRangeLabels() {
    return [elements.fromDateInput.value || "from", elements.toDateInput.value || "to"];
}

function toInputDate(date) {
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, "0")}-${String(date.getDate()).padStart(2, "0")}`;
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

function shortDate(value) {
    return value?.slice(5) || "-";
}

function formatStatus(status) {
    const mapping = {
        AWAITING_SHIPMENT: "待发货",
        AWAITING_COLLECTION: "待揽收",
        UNPAID: "未支付",
        CANCELLED: "已取消",
        COMPLETED: "已完成",
        DELIVERED: "已送达"
    };
    return mapping[status] || status || "-";
}

function formatClassification(classification) {
    const mapping = {
        included: "计入销售",
        unpaid: "未支付",
        cancelled: "已取消",
        refunded_or_closed: "退款/关闭"
    };
    return mapping[classification] || classification || "-";
}

function csvEscape(value) {
    const text = String(value ?? "");
    if (text.includes(",") || text.includes("\"") || text.includes("\n")) {
        return `"${text.replaceAll("\"", "\"\"")}"`;
    }
    return text;
}

function emptyChart(message) {
    return `<div class="empty-panel">${escapeHtml(message)}</div>`;
}

function toLinePath(points) {
    return points.map((point, index) => `${index === 0 ? "M" : "L"} ${point.x} ${point.y}`).join(" ");
}

function describeArc(cx, cy, r, startAngle, endAngle) {
    const start = polarToCartesian(cx, cy, r, endAngle);
    const end = polarToCartesian(cx, cy, r, startAngle);
    const largeArcFlag = endAngle - startAngle <= 180 ? "0" : "1";
    return `M ${start.x} ${start.y} A ${r} ${r} 0 ${largeArcFlag} 0 ${end.x} ${end.y}`;
}

function polarToCartesian(cx, cy, r, angle) {
    const radians = ((angle - 90) * Math.PI) / 180;
    return {
        x: cx + (r * Math.cos(radians)),
        y: cy + (r * Math.sin(radians))
    };
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll("\"", "&quot;")
        .replaceAll("'", "&#39;");
}

function renderOrderList(target, rows, currency, detailed) {
    if (!target) {
        return;
    }

    if (!rows.length) {
        target.innerHTML = `<div class="empty-panel">暂无订单数据</div>`;
        return;
    }

    target.innerHTML = rows.map((item) => `
        <article class="order-card ${detailed && item.exclusionReason ? "excluded" : ""}">
            <div class="order-head">
                <div>
                    <span class="order-tag ${item.exclusionReason ? "excluded" : "muted"}">${escapeHtml(item.exclusionReason ? formatClassification(item.exclusionReason) : "计入销售")}</span>
                    <strong>#${escapeHtml(item.orderId)}</strong>
                </div>
                <div class="order-amount">${formatMoney(item.grossWithDiscount, currency)}</div>
            </div>
            <div class="order-body">
                <div class="order-line"><span>用户</span><strong>${escapeHtml(item.buyerHandle || item.buyerUserId || item.buyerEmail || "未识别")}</strong></div>
                <div class="order-line"><span>状态</span><strong>${escapeHtml(formatStatus(item.status))}</strong></div>
                <div class="order-line"><span>支付方式</span><strong>${escapeHtml(item.paymentMethod || "-")}</strong></div>
                <div class="order-line"><span>支付时间</span><strong>${escapeHtml(item.paidAtLocal || "-")}</strong></div>
                ${detailed ? `<div class="order-line wide"><span>商品摘要</span><strong>${escapeHtml(item.primaryProductName || "-")}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>链接归因</span><strong>${escapeHtml(item.linkAttributionLabel || "未归因")}</strong></div>` : ""}
                ${detailed ? `<div class="order-line wide"><span>链接 URL</span><strong>${escapeHtml(item.linkAttributionUrl || "-")}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>回款状态</span><strong>${escapeHtml(item.settlementState || "-")}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>平台费</span><strong>${formatMoney(item.estimatedPlatformFeeAmount || 0, currency)}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>物流费</span><strong>${formatMoney(item.estimatedLogisticsCostAmount || 0, currency)}</strong></div>` : ""}
                ${detailed ? `<div class="order-line"><span>已算好运费</span><strong>${item.hasCalculatedShippingFee ? formatMoney(item.calculatedShippingFeeAmount || 0, currency) : "未出"}</strong></div>` : ""}
            </div>
        </article>`).join("");
}

function buildSummaryClipboard(summary) {
    const bestLink = (summary.linkAttributionBreakdown || [])[0];
    return [
        `店铺：${summary.storeName}`,
        `区间：${summary.fromUtc.slice(0, 10)} ~ ${summary.toUtc.slice(0, 10)}`,
        `有效订单数：${summary.overview.includedOrderCount}`,
        `实付金额：${formatMoney(summary.overview.includedPaidAmount, summary.currency)}`,
        `TikTok 折扣：${formatMoney(summary.overview.includedTikTokDiscountAmount, summary.currency)}`,
        `成交额：${formatMoney(summary.overview.includedGrossWithDiscount, summary.currency)}`,
        `预估可回款：${formatMoney(summary.reconciliation.estimatedReceivableAfterEstimatedShippingAmount || 0, summary.currency)}`,
        `实际可回款：${formatMoney(summary.reconciliation.actualReceivableAfterActualShippingAmount || 0, summary.currency)}`,
        `未支付提醒：${summary.unpaidReminders.length}`,
        `黑名单候选：${summary.blacklistCandidates.length}`,
        bestLink ? `最强链接：${bestLink.label} / ${formatMoney(bestLink.grossWithDiscount, summary.currency)}` : "最强链接：暂无"
    ].join("\n");
}

function buildOrderAmountsClipboard(summary) {
    const header = ["订单号", "用户名", "链接归因", "实付金额", "TikTok折扣", "成交额"];
    const rows = (summary.includedOrders || []).map((item) => [
        item.orderId,
        item.buyerHandle || item.buyerUserId || item.buyerEmail || "",
        item.linkAttributionLabel || "",
        String(item.paidAmount || 0),
        String(item.tikTokDiscountAmount || 0),
        String(item.grossWithDiscount || 0)
    ]);
    return [header, ...rows].map((row) => row.join("\t")).join("\n");
}

function buildCsv(summary) {
    const header = ["订单号", "用户名", "BuyerUserId", "订单状态", "支付方式", "支付时间", "链接归因", "链接URL", "实付金额", "TikTok折扣", "成交额"];
    const rows = (summary.includedOrders || []).map((item) => [
        item.orderId,
        item.buyerHandle || "",
        item.buyerUserId || "",
        item.status || "",
        item.paymentMethod || "",
        item.paidAtLocal || "",
        item.linkAttributionLabel || "",
        item.linkAttributionUrl || "",
        item.paidAmount || 0,
        item.tikTokDiscountAmount || 0,
        item.grossWithDiscount || 0
    ]);

    return [header, ...rows].map((row) => row.map(csvEscape).join(",")).join("\n");
}

