// ==UserScript==
// @name         TikTok Seller Center -> Print Studio
// @namespace    http://localhost:5038/
// @version      1.4.1
// @description  Sync visible customer handles from Seller Center back to the local Print Studio by order ID.
// @match        https://seller*.tiktok.com/*
// @match        https://seller*.tiktokglobalshop.com/*
// @match        https://*.seller.tiktokglobalshop.com/*
// @match        https://seller*.tiktokshopglobalselling.com/*
// @match        https://*.seller.tiktokshopglobalselling.com/*
// @match        https://*.tiktokshopglobalselling.com/*
// @run-at       document-idle
// @grant        none
// ==/UserScript==

(function () {
  "use strict";

  const LOCAL_APP_URL = "http://localhost:5038";
  const CAPTURE_URL = `${LOCAL_APP_URL}/api/bridge/seller-center/capture`;
  const PING_URL = `${LOCAL_APP_URL}/api/bridge/seller-center/ping`;
  const SIGNAL_URL = `${LOCAL_APP_URL}/api/bridge/seller-center/signal.js`;
  const USERNAME_LABELS = ["用户名", "Username", "ユーザー名"];
  const CHAT_LABELS = ["开始聊天", "Chat", "チャット"];
  const ORDER_ID_LABELS = ["订单 ID", "Order ID", "注文 ID"];
  const ORDER_WORKSPACE_MARKERS = ["待发货", "Awaiting Shipment", "订单 ID", "Order ID", "开始聊天", "Chat"];
  const AUTO_REFRESH_MS = 45000;
  const PING_INTERVAL_MS = 15000;
  const SIGNAL_INTERVAL_MS = 6000;
  const RELOAD_COOLDOWN_MS = 10000;
  const INTERACTION_GRACE_MS = 12000;

  const sentKeys = new Set();
  let lastStatus = "";
  let lastInteractionAt = Date.now();
  let lastReloadAt = Number(window.sessionStorage.getItem("__print_studio_last_reload__") || "0");
  let lastPendingOrderTimestamp = Number(window.sessionStorage.getItem("__print_studio_last_pending_order_ts__") || "0");
  let lastPingAt = 0;
  let lastSignalCheckAt = 0;
  let pendingReloadReason = "";

  function normalizeText(value) {
    return String(value || "").replace(/\s+/g, " ").trim();
  }

  function isVisible(element) {
    if (!(element instanceof HTMLElement)) {
      return false;
    }

    const style = window.getComputedStyle(element);
    return style.display !== "none" && style.visibility !== "hidden" && element.offsetParent !== null;
  }

  function renderStatus(text, tone) {
    const id = "__print_studio_bridge_status__";
    let banner = document.getElementById(id);
    if (!banner) {
      banner = document.createElement("div");
      banner.id = id;
      banner.style.cssText = [
        "position:fixed !important",
        "top:12px !important",
        "left:50% !important",
        "transform:translateX(-50%) !important",
        "z-index:2147483647 !important",
        "padding:12px 18px !important",
        "border-radius:14px !important",
        "font-size:13px !important",
        "font-family:Segoe UI, Arial, sans-serif !important",
        "border:2px solid rgba(255,255,255,.65) !important",
        "box-shadow:0 14px 30px rgba(0,0,0,.28) !important",
        "pointer-events:none !important",
        "max-width:560px !important",
        "line-height:1.35 !important",
        "text-align:center !important",
        "white-space:normal !important"
      ].join(";");
      banner.style.fontWeight = "700";
      (document.body || document.documentElement).appendChild(banner);
    }

    if (text === lastStatus) {
      return;
    }

    lastStatus = text;
    banner.textContent = text;

    if (tone === "error") {
      banner.style.background = "#8b3828";
    } else if (tone === "warn") {
      banner.style.background = "#7b5a1f";
    } else {
      banner.style.background = "#0f5d54";
    }

    banner.style.color = "#fff";
  }

  function markInteraction() {
    lastInteractionAt = Date.now();
  }

  function extractOrderIdFromText(text) {
    if (!text) {
      return "";
    }

    const explicitMatch = text.match(/(?:订单\s*ID|Order\s*ID|注文\s*ID)\s*[:：]?\s*(\d{15,20})/i);
    if (explicitMatch) {
      return explicitMatch[1];
    }

    const genericMatch = text.match(/\b\d{15,20}\b/);
    return genericMatch ? genericMatch[0] : "";
  }

  function collectContainerTexts(container) {
    if (!container) {
      return [];
    }

    return [...container.querySelectorAll("*")]
      .filter(isVisible)
      .map((element) => normalizeText(element.textContent))
      .filter((text) => text.length > 0);
  }

  function looksLikeNickname(value) {
    return /^[A-Za-z0-9._-]{3,40}$/.test(value) && !/^\d+$/.test(value);
  }

  function findValueNearLabel(labels) {
    const elements = [...document.querySelectorAll("*")].filter(isVisible);
    const labelElement = elements.find((element) => labels.includes(normalizeText(element.textContent)));
    if (!labelElement) {
      return "";
    }

    const containers = [
      labelElement.parentElement,
      labelElement.closest("section"),
      labelElement.closest("div")
    ].filter(Boolean);

    for (const container of containers) {
      const candidates = collectContainerTexts(container)
        .filter((text) => !labels.includes(text))
        .filter((text) => !ORDER_ID_LABELS.includes(text))
        .filter((text) => text.length > 1);

      for (const text of candidates) {
        if (looksLikeNickname(text)) {
          return text;
        }
      }
    }

    return "";
  }

  function findOrderIdFromPage() {
    const urlMatch = location.href.match(/\b\d{15,20}\b/);
    if (urlMatch) {
      return urlMatch[0];
    }

    return extractOrderIdFromText(normalizeText(document.body ? document.body.innerText : ""));
  }

  function buildListCaptureFromActionNode(actionNode) {
    let container = actionNode;
    for (let i = 0; i < 7 && container; i += 1) {
      const text = normalizeText(container.textContent);
      const orderId = extractOrderIdFromText(text);
      if (orderId) {
        const candidates = collectContainerTexts(container)
          .filter((value) => looksLikeNickname(value))
          .filter((value) => !CHAT_LABELS.includes(value));

        if (candidates.length > 0) {
          return {
            orderId,
            buyerNickname: candidates[0],
            buyerName: "",
            sourceUrl: location.href
          };
        }

        break;
      }

      container = container.parentElement;
    }

    return null;
  }

  function createCaptures() {
    const captures = [];
    const seenOrderIds = new Set();

    const detailOrderId = findOrderIdFromPage();
    const detailNickname = findValueNearLabel(USERNAME_LABELS);
    if (detailOrderId && detailNickname) {
      captures.push({
        orderId: detailOrderId,
        buyerNickname: detailNickname,
        buyerName: "",
        sourceUrl: location.href
      });
      seenOrderIds.add(detailOrderId);
    }

    const actionNodes = [...document.querySelectorAll("*")]
      .filter(isVisible)
      .filter((element) => CHAT_LABELS.includes(normalizeText(element.textContent)));

    for (const actionNode of actionNodes) {
      const capture = buildListCaptureFromActionNode(actionNode);
      if (!capture || seenOrderIds.has(capture.orderId)) {
        continue;
      }

      captures.push(capture);
      seenOrderIds.add(capture.orderId);
    }

    return captures;
  }

  function pingLocalApp() {
    const now = Date.now();
    if (now - lastPingAt < PING_INTERVAL_MS) {
      return;
    }

    lastPingAt = now;
    const image = new Image();
    image.src = `${PING_URL}?sourceUrl=${encodeURIComponent(location.href)}&_=${now}`;
  }

  function sendCapture(capture) {
    const key = `${capture.orderId}|${capture.buyerNickname}`;
    if (sentKeys.has(key)) {
      return;
    }

    sentKeys.add(key);
    const params = new URLSearchParams({
      orderId: capture.orderId,
      buyerNickname: capture.buyerNickname,
      buyerName: capture.buyerName || "",
      sourceUrl: capture.sourceUrl || location.href
    });

    const image = new Image();
    image.onload = () => renderStatus(`已同步：${capture.buyerNickname}`, "ok");
    image.onerror = () => renderStatus("本地 Print Studio 未响应", "error");
    image.src = `${CAPTURE_URL}?${params.toString()}`;
    renderStatus(`正在同步：${capture.buyerNickname}`, "ok");
  }

  function isLikelyOrderWorkspace() {
    const bodyText = normalizeText(document.body ? document.body.innerText : "");
    return ORDER_WORKSPACE_MARKERS.some((marker) => bodyText.includes(marker));
  }

  function canReloadNow() {
    if (document.hidden) {
      return true;
    }

    return Date.now() - lastInteractionAt >= INTERACTION_GRACE_MS;
  }

  function triggerReload(reason) {
    const now = Date.now();
    if (now - lastReloadAt < RELOAD_COOLDOWN_MS) {
      return;
    }

    pendingReloadReason = "";
    lastReloadAt = now;
    window.sessionStorage.setItem("__print_studio_last_reload__", String(lastReloadAt));
    renderStatus(reason, "warn");
    window.location.reload();
  }

  function queueReload(reason) {
    pendingReloadReason = reason;
    if (canReloadNow()) {
      triggerReload(reason);
    } else {
      renderStatus(`${reason}，等你停下来后自动刷新`, "warn");
    }
  }

  function consumePendingReload() {
    if (!pendingReloadReason) {
      return;
    }

    if (!canReloadNow()) {
      return;
    }

    const reason = pendingReloadReason;
    pendingReloadReason = "";
    triggerReload(reason);
  }

  function autoRefreshIfIdle() {
    if (!isLikelyOrderWorkspace()) {
      return;
    }

    const now = Date.now();
    if (now - lastInteractionAt < AUTO_REFRESH_MS) {
      return;
    }

    if (now - lastReloadAt < AUTO_REFRESH_MS) {
      return;
    }

    triggerReload("页面空闲，正在自动刷新订单列表");
  }

  function pollBridgeSignal() {
    const now = Date.now();
    if (now - lastSignalCheckAt < SIGNAL_INTERVAL_MS) {
      return;
    }

    lastSignalCheckAt = now;
    const script = document.createElement("script");
    script.src = `${SIGNAL_URL}?_=${now}`;
    script.async = true;

    script.onload = () => {
      try {
        const signal = window.__printStudioBridgeSignal || {};
        const pendingCount = Number(signal.pendingBridgeCount || 0);
        const latestPendingAt = Date.parse(signal.latestPendingBridgeOrderAtUtc || "");
        const latestPendingOrderId = String(signal.latestPendingBridgeOrderId || "");

        if (pendingCount > 0 && Number.isFinite(latestPendingAt) && latestPendingAt > lastPendingOrderTimestamp) {
          lastPendingOrderTimestamp = latestPendingAt;
          window.sessionStorage.setItem("__print_studio_last_pending_order_ts__", String(latestPendingAt));
          queueReload(`检测到新订单 ${latestPendingOrderId || ""}，准备刷新列表`);
          return;
        }

        if (!lastStatus) {
          renderStatus("桥接在线，等待识别客户ID", "warn");
        }
      } finally {
        script.remove();
      }
    };

    script.onerror = () => {
      script.remove();
    };

    (document.head || document.documentElement).appendChild(script);
  }

  function scan() {
    pingLocalApp();
    pollBridgeSignal();
    consumePendingReload();

    const captures = createCaptures();
    if (captures.length === 0) {
      renderStatus("桥接在线，等待识别客户ID", "warn");
      return;
    }

    captures.forEach(sendCapture);
  }

  console.log("[Print Studio Bridge] script loaded");
  renderStatus("桥接脚本已启动", "ok");
  scan();

  ["pointerdown", "keydown", "wheel", "scroll"].forEach((eventName) => {
    window.addEventListener(eventName, markInteraction, { passive: true });
  });

  document.addEventListener("visibilitychange", () => {
    if (document.hidden) {
      consumePendingReload();
    }
  });

  new MutationObserver(() => scan()).observe(document.body, {
    childList: true,
    subtree: true
  });

  window.setInterval(() => {
    scan();
    autoRefreshIfIdle();
  }, 4000);
})();
