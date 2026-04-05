namespace TikTokOrderPrinter.Services;

public static class SellerCenterBridgeScriptBuilder
{
    public static string Build(string localAppBaseUrl)
    {
        var normalizedBaseUrl = localAppBaseUrl.TrimEnd('/');

        return $$"""
// ==UserScript==
// @name         TikTok Seller Center -> Print Studio
// @namespace    {{normalizedBaseUrl}}/
// @version      1.5.8
// @description  Sync visible customer handles from Seller Center back to the local Print Studio by order ID.
// @match        https://seller*.tiktok.com/*
// @match        https://seller*.tiktokglobalshop.com/*
// @match        https://*.seller.tiktokglobalshop.com/*
// @match        https://seller*.tiktokshopglobalselling.com/*
// @match        https://*.seller.tiktokshopglobalselling.com/*
// @match        https://*.tiktokshopglobalselling.com/*
// @run-at       document-idle
// @grant        GM_xmlhttpRequest
// @connect      localhost
// @connect      127.0.0.1
// ==/UserScript==

(function () {
  "use strict";

  const LOCAL_APP_URL = "{{normalizedBaseUrl}}";
  const CAPTURE_URL = `${LOCAL_APP_URL}/api/bridge/seller-center/capture`;
  const PING_URL = `${LOCAL_APP_URL}/api/bridge/seller-center/ping`;
  const SIGNAL_URL = `${LOCAL_APP_URL}/api/bridge/seller-center/signal`;
  const USERNAME_LABELS = ["\u7528\u6237\u540d", "username", "\u30e6\u30fc\u30b6\u30fc\u540d"];
  const CHAT_LABELS = ["\u5f00\u59cb\u804a\u5929", "chat", "\u30c1\u30e3\u30c3\u30c8"];
  const ORDER_ID_LABELS = ["\u8ba2\u5355 id", "order id", "\u6ce8\u6587 id"];
  const ORDER_WORKSPACE_MARKERS = ["\u5f85\u53d1\u8d27", "awaiting shipment", "\u8ba2\u5355 id", "order id", "\u5f00\u59cb\u804a\u5929", "chat"];
  const HANDLE_BLACKLIST = new Set([
    "paypay",
    "paypal",
    "credit/debit card",
    "convenientstore",
    "wise express - dcs",
    "wise express",
    "dcs",
    "tt virtual express",
    "\u5e73\u53f0\u53d1\u8d27",
    "\u5f85\u53d1\u8d27",
    "\u5168\u7403\u6807\u51c6\u8fd0\u8f93\u670d\u52a1"
  ]);
  const SCAN_INTERVAL_MS = 4000;
  const PING_INTERVAL_MS = 15000;
  const SIGNAL_INTERVAL_MS = 6000;
  const RELOAD_COOLDOWN_MS = 10000;
  const INTERACTION_GRACE_MS = 12000;

  const sentKeys = new Set();
  let lastStatus = "";
  let lastTone = "ok";
  let lastInteractionAt = Date.now();
  let lastReloadAt = Number(window.sessionStorage.getItem("__print_studio_last_reload__") || "0");
  let lastPendingOrderTimestamp = Number(window.sessionStorage.getItem("__print_studio_last_pending_order_ts__") || "0");
  let lastPingAt = 0;
  let lastSignalCheckAt = 0;
  let pendingReloadReason = "";
  let lastCaptureCount = 0;
  let signalPrimed = false;

  function normalizeText(value) {
    return String(value || "").replace(/\s+/g, " ").trim();
  }

  function normalizeLower(value) {
    return normalizeText(value).toLowerCase();
  }

  function containsAny(text, needles) {
    const normalized = normalizeLower(text);
    return needles.some((needle) => normalized.includes(needle));
  }

  function isVisible(element) {
    if (!(element instanceof HTMLElement)) {
      return false;
    }

    const style = window.getComputedStyle(element);
    return style.display !== "none" && style.visibility !== "hidden" && style.opacity !== "0";
  }

  function ensureBanner() {
    const id = "__print_studio_bridge_status__";
    let banner = document.getElementById(id);
    if (banner) {
      return banner;
    }

    banner = document.createElement("div");
    banner.id = id;
    banner.style.cssText = [
      "position:fixed",
      "top:12px",
      "left:12px",
      "z-index:2147483647",
      "padding:12px 16px",
      "border-radius:12px",
      "font:12px/1.45 Segoe UI, Arial, sans-serif",
      "font-weight:700",
      "border:2px solid rgba(255,255,255,.7)",
      "box-shadow:0 16px 32px rgba(0,0,0,.28)",
      "pointer-events:none",
      "max-width:360px",
      "white-space:pre-line",
      "color:#fff"
    ].join(";");
    (document.body || document.documentElement).appendChild(banner);
    return banner;
  }

  function renderStatus(text, tone) {
    const banner = ensureBanner();
    if (text === lastStatus && tone === lastTone) {
      return;
    }

    lastStatus = text;
    lastTone = tone;
    banner.textContent = text;
    banner.style.background = tone === "error"
      ? "#8b3828"
      : tone === "warn"
        ? "#7b5a1f"
        : "#0f5d54";
  }

  function debug(message, extra) {
    console.log("[Print Studio Bridge]", message, extra || "");
  }

  function markInteraction() {
    lastInteractionAt = Date.now();
  }

  function extractOrderIdFromText(text) {
    if (!text) {
      return "";
    }

    const explicitMatch = text.match(/(?:\u8ba2\u5355\s*id|order\s*id|\u6ce8\u6587\s*id)\s*[:\uff1a]?\s*(\d{15,20})/i);
    if (explicitMatch) {
      return explicitMatch[1];
    }

    const genericMatch = text.match(/\b\d{15,20}\b/);
    return genericMatch ? genericMatch[0] : "";
  }

  function extractExplicitOrderIdFromText(text) {
    if (!text) {
      return "";
    }

    const explicitMatch = text.match(/(?:\u8ba2\u5355\s*id|order\s*id|\u6ce8\u6587\s*id)\s*[:\uff1a]?\s*(\d{15,20})/i);
    return explicitMatch ? explicitMatch[1] : "";
  }

  function looksLikeHandle(value) {
    return /^[A-Za-z0-9._-]{3,40}$/.test(value) && !/^\d+$/.test(value);
  }

  function isMaskedToken(value) {
    return /^[.*_-]{3,}$/.test(value);
  }

  function isBlacklistedHandle(value) {
    return HANDLE_BLACKLIST.has(normalizeLower(value));
  }

  function visibleTextEntries(container) {
    if (!container) {
      return [];
    }

    return [...container.querySelectorAll("*")]
      .filter(isVisible)
      .map((element) => ({
        element,
        text: normalizeText(element.textContent)
      }))
      .filter((entry) => entry.text.length > 0 && entry.text.length <= 64);
  }

  function hasVisibleDescendantWithSameOrderId(element, orderId) {
    return [...element.querySelectorAll("*")]
      .filter((node) => node instanceof HTMLElement)
      .filter(isVisible)
      .some((node) => node !== element && extractExplicitOrderIdFromText(normalizeText(node.textContent)) === orderId);
  }

  function hasVisibleTextChild(element) {
    return [...element.children]
      .filter((child) => child instanceof HTMLElement)
      .some((child) => isVisible(child) && normalizeText(child.textContent).length > 0);
  }

  function leafTextEntries(container) {
    if (!container) {
      return [];
    }

    return [...container.querySelectorAll("*")]
      .filter((element) => element instanceof HTMLElement)
      .filter(isVisible)
      .filter((element) => !hasVisibleTextChild(element))
      .map((element) => ({
        element,
        text: normalizeText(element.textContent),
        rect: element.getBoundingClientRect()
      }))
      .filter((entry) => entry.text.length > 0 && entry.text.length <= 120 && entry.rect.width > 0 && entry.rect.height > 0);
  }

  function collectContainerTexts(container) {
    return visibleTextEntries(container).map((entry) => entry.text);
  }

  function findOrderIdFromPage() {
    const urlMatch = location.href.match(/\b\d{15,20}\b/);
    if (urlMatch) {
      return urlMatch[0];
    }

    return extractOrderIdFromText(normalizeText(document.body ? document.body.innerText : ""));
  }

  function findValueNearLabel(labels) {
    const normalizedLabels = labels.map((label) => normalizeLower(label));
    const entries = visibleTextEntries(document.body);
    const labelEntry = entries.find((entry) => normalizedLabels.includes(normalizeLower(entry.text)));
    if (!labelEntry) {
      return "";
    }

    const containers = [
      labelEntry.element.parentElement,
      labelEntry.element.closest("section"),
      labelEntry.element.closest("div")
    ].filter(Boolean);

    for (const container of containers) {
      const candidates = collectContainerTexts(container)
        .filter((text) => !normalizedLabels.includes(normalizeLower(text)))
        .filter((text) => !ORDER_ID_LABELS.includes(normalizeLower(text)))
        .filter(looksLikeHandle)
        .filter((text) => !isBlacklistedHandle(text));

      if (candidates.length > 0) {
        return candidates[0];
      }
    }

    return "";
  }

  function chooseHandleNearActionNode(container, actionNode) {
    const entries = visibleTextEntries(container);
    if (entries.length === 0) {
      return "";
    }

    const actionIndex = entries.findIndex((entry) => entry.element === actionNode || containsAny(entry.text, CHAT_LABELS));
    const fromIndex = actionIndex >= 0 ? Math.max(0, actionIndex - 10) : 0;
    const toIndex = actionIndex >= 0 ? actionIndex : entries.length;
    const candidates = entries
      .slice(fromIndex, toIndex)
      .map((entry) => entry.text)
      .filter(looksLikeHandle)
      .filter((text) => !isBlacklistedHandle(text));

    return candidates.length > 0 ? candidates[candidates.length - 1] : "";
  }

  function chooseHandleFromTexts(texts) {
    const candidates = texts
      .filter(looksLikeHandle)
      .filter((text) => !isBlacklistedHandle(text));

    if (candidates.length === 0) {
      return "";
    }

    return candidates[candidates.length - 1];
  }

  function extractHandleTokens(text) {
    const direct = normalizeText(text);
    const parts = direct.split(/[\s|/\\,;:()[\]{}<>]+/);
    return parts
      .map((part) => normalizeText(part))
      .filter((part) => part.length > 0)
      .filter(looksLikeHandle)
      .filter((part) => !isMaskedToken(part))
      .filter((part) => !isBlacklistedHandle(part));
  }

  function findRowRoot(startNode) {
    let node = startNode;
    let best = null;

    for (let i = 0; i < 10 && node; i += 1) {
      const text = normalizeText(node.textContent);
      const orderId = extractExplicitOrderIdFromText(text);
      if (orderId) {
        best = node;
      }

      node = node.parentElement;
    }

    return best;
  }

  function buildCaptureFromRowRoot(rowRoot) {
    if (!rowRoot) {
      return null;
    }

    const rowText = normalizeText(rowRoot.textContent);
    const orderId = extractExplicitOrderIdFromText(rowText);
    if (!orderId) {
      return null;
    }

    const texts = collectContainerTexts(rowRoot);
    const buyerNickname = chooseHandleFromTexts(texts);
    if (!buyerNickname) {
      return null;
    }

    return {
      orderId,
      buyerNickname,
      buyerName: "",
      sourceUrl: location.href
    };
  }

  function buildLineAnchoredCaptures(seenOrderIds) {
    const captures = [];
    const groups = new Map();
    const entries = leafTextEntries(document.body);

    for (const entry of entries) {
      const key = String(Math.round(entry.rect.top / 8) * 8);
      if (!groups.has(key)) {
        groups.set(key, []);
      }

      groups.get(key).push(entry);
    }

    for (const group of groups.values()) {
      const ordered = group.sort((a, b) => a.rect.left - b.rect.left);
      const lineText = ordered.map((entry) => entry.text).join(" ");
      const orderId = extractExplicitOrderIdFromText(lineText);
      if (!orderId || seenOrderIds.has(orderId)) {
        continue;
      }

      const handleCandidates = ordered
        .flatMap((entry) => extractHandleTokens(entry.text).map((token) => ({ token, left: entry.rect.left })))
        .sort((a, b) => a.left - b.left);

      if (handleCandidates.length === 0) {
        continue;
      }

      const buyerNickname = handleCandidates[handleCandidates.length - 1].token;
      captures.push({
        orderId,
        buyerNickname,
        buyerName: "",
        sourceUrl: location.href
      });
      seenOrderIds.add(orderId);
    }

    return captures;
  }

  function buildOrderHeaderCaptures(seenOrderIds) {
    const captures = [];
    const entries = visibleTextEntries(document.body)
      .map((entry) => ({
        ...entry,
        rect: entry.element.getBoundingClientRect()
      }))
      .filter((entry) => entry.rect.width > 0 && entry.rect.height > 0);

    const orderEntries = entries
      .map((entry) => ({
        ...entry,
        orderId: extractExplicitOrderIdFromText(entry.text)
      }))
      .filter((entry) => !!entry.orderId)
      .filter((entry) => !hasVisibleDescendantWithSameOrderId(entry.element, entry.orderId))
      .sort((a, b) => a.rect.top - b.rect.top || a.rect.left - b.rect.left);

    const handleEntries = entries.filter((entry) =>
      extractHandleTokens(entry.text).length > 0 &&
      !CHAT_LABELS.includes(normalizeLower(entry.text)));

    for (const orderEntry of orderEntries) {
      const orderId = orderEntry.orderId;
      if (!orderId || seenOrderIds.has(orderId)) {
        continue;
      }

      const sameBandHandles = handleEntries
        .filter((entry) => entry.rect.left > orderEntry.rect.left)
        .filter((entry) => Math.abs(entry.rect.top - orderEntry.rect.top) <= 40)
        .filter((entry) => entry.rect.left - orderEntry.rect.left >= 120)
        .flatMap((entry) => extractHandleTokens(entry.text).map((token) => ({ token, left: entry.rect.left })))
        .sort((a, b) => a.left - b.left);

      if (sameBandHandles.length === 0) {
        continue;
      }

      captures.push({
        orderId,
        buyerNickname: sameBandHandles[sameBandHandles.length - 1].token,
        buyerName: "",
        sourceUrl: location.href
      });
      seenOrderIds.add(orderId);
    }

    return captures;
  }

  function buildDetailCapture() {
    const orderId = findOrderIdFromPage();
    const buyerNickname = findValueNearLabel(USERNAME_LABELS);
    if (!orderId || !buyerNickname) {
      return null;
    }

    return {
      orderId,
      buyerNickname,
      buyerName: "",
      sourceUrl: location.href
    };
  }

  function createCaptures() {
    const captures = [];
    const seenOrderIds = new Set();

    const detailCapture = buildDetailCapture();
    if (detailCapture) {
      captures.push(detailCapture);
      seenOrderIds.add(detailCapture.orderId);
    }

    buildOrderHeaderCaptures(seenOrderIds).forEach((capture) => captures.push(capture));
    buildLineAnchoredCaptures(seenOrderIds).forEach((capture) => captures.push(capture));

    return captures;
  }

  function callLocal(url, onSuccess, onError) {
    if (typeof GM_xmlhttpRequest === "function") {
      GM_xmlhttpRequest({
        method: "GET",
        url,
        timeout: 6000,
        onload(response) {
          if (response.status >= 200 && response.status < 400) {
            onSuccess(response);
            return;
          }

          onError(response);
        },
        onerror(error) {
          onError(error);
        },
        ontimeout(error) {
          onError(error);
        }
      });
      return;
    }

    fetch(url, { mode: "no-cors", credentials: "omit" })
      .then(() => onSuccess({ status: 0, responseText: "" }))
      .catch(onError);
  }

  function pingLocalApp() {
    const now = Date.now();
    if (now - lastPingAt < PING_INTERVAL_MS) {
      return;
    }

    lastPingAt = now;
    const url = `${PING_URL}?sourceUrl=${encodeURIComponent(location.href)}&_=${now}`;
    callLocal(url, () => {
      if (!lastStatus) {
        renderStatus("\u6865\u63a5\u5728\u7ebf\uff0c\u7b49\u5f85\u8bc6\u522b\u5ba2\u6237ID", "warn");
      }
    }, () => {
      renderStatus("\u672c\u5730 Print Studio \u672a\u54cd\u5e94", "error");
    });
  }

  function sendCapture(capture) {
    const key = `${capture.orderId}|${capture.buyerNickname}`;
    if (sentKeys.has(key)) {
      return;
    }

    sentKeys.add(key);
    renderStatus(`\u6b63\u5728\u540c\u6b65\uff1a${capture.buyerNickname}`, "ok");

    const params = new URLSearchParams({
      orderId: capture.orderId,
      buyerNickname: capture.buyerNickname,
      buyerName: capture.buyerName || "",
      sourceUrl: capture.sourceUrl || location.href
    });

    callLocal(`${CAPTURE_URL}?${params.toString()}`, () => {
      renderStatus(`\u5df2\u540c\u6b65\uff1a${capture.buyerNickname}`, "ok");
    }, () => {
      renderStatus("\u672c\u5730 Print Studio \u672a\u54cd\u5e94", "error");
    });
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
      return;
    }

    renderStatus(`${reason}\n\u7b49\u4f60\u505c\u4e0b\u6765\u540e\u81ea\u52a8\u5237\u65b0`, "warn");
  }

  function consumePendingReload() {
    if (!pendingReloadReason || !canReloadNow()) {
      return;
    }

    const reason = pendingReloadReason;
    pendingReloadReason = "";
    triggerReload(reason);
  }

  function pollBridgeSignal() {
    const now = Date.now();
    if (now - lastSignalCheckAt < SIGNAL_INTERVAL_MS) {
      return;
    }

    lastSignalCheckAt = now;
    callLocal(`${SIGNAL_URL}?_=${now}`, (response) => {
      let signal = null;
      try {
        signal = JSON.parse(response.responseText || "{}");
      } catch (error) {
        debug("signal parse failed", error);
        return;
      }

      const pendingCount = Number(signal.pendingBridgeCount || 0);
      const latestPendingAt = Date.parse(signal.latestPendingBridgeOrderAtUtc || "");
      const latestPendingOrderId = String(signal.latestPendingBridgeOrderId || "");

      if (!signalPrimed) {
        signalPrimed = true;
        if (Number.isFinite(latestPendingAt)) {
          lastPendingOrderTimestamp = latestPendingAt;
          window.sessionStorage.setItem("__print_studio_last_pending_order_ts__", String(latestPendingAt));
        }

        if (!lastStatus) {
          renderStatus("\u6865\u63a5\u5728\u7ebf\uff0c\u7b49\u5f85\u65b0\u8ba2\u5355\u4fe1\u53f7", "warn");
        }

        return;
      }

      if (pendingCount > 0 && Number.isFinite(latestPendingAt) && latestPendingAt > lastPendingOrderTimestamp) {
        lastPendingOrderTimestamp = latestPendingAt;
        window.sessionStorage.setItem("__print_studio_last_pending_order_ts__", String(latestPendingAt));
        queueReload(`\u68c0\u6d4b\u5230\u65b0\u8ba2\u5355 ${latestPendingOrderId || ""}\uff0c\u51c6\u5907\u5237\u65b0\u5217\u8868`);
        return;
      }

      if (!lastStatus) {
        renderStatus("\u6865\u63a5\u5728\u7ebf\uff0c\u7b49\u5f85\u8bc6\u522b\u5ba2\u6237ID", "warn");
      }
    }, () => {
      renderStatus("\u672c\u5730 Print Studio \u672a\u54cd\u5e94", "error");
    });
  }

  function scan() {
    document.documentElement.setAttribute("data-print-studio-bridge", "1.5.8");
    pingLocalApp();
    pollBridgeSignal();
    consumePendingReload();

    const captures = createCaptures();
    lastCaptureCount = captures.length;
    if (captures.length === 0) {
      renderStatus(`\u6865\u63a5\u5728\u7ebf\uff0c\u672c\u9875\u8fd8\u6ca1\u6293\u5230\u5ba2\u6237ID\n\u53ef\u89c1\u8ba2\u5355\u6b63\u5728\u91cd\u8bd5\u626b\u63cf`, "warn");
      return;
    }

    renderStatus(`\u6865\u63a5\u5728\u7ebf\uff0c\u672c\u9875\u5df2\u6293\u5230 ${captures.length} \u6761\u5ba2\u6237ID`, "ok");
    captures.forEach(sendCapture);
  }

  debug("script loaded", { href: location.href });
  renderStatus("\u6865\u63a5\u811a\u672c\u5df2\u542f\u52a8", "ok");
  ensureBanner();

  ["pointerdown", "keydown", "wheel", "scroll", "mousedown"].forEach((eventName) => {
    window.addEventListener(eventName, markInteraction, { passive: true });
  });

  document.addEventListener("visibilitychange", () => {
    if (document.hidden) {
      consumePendingReload();
    }
  });

  if (document.body) {
    new MutationObserver(() => scan()).observe(document.body, {
      childList: true,
      subtree: true
    });
  }

  scan();
  window.setInterval(() => {
    scan();
  }, SCAN_INTERVAL_MS);
})();
""";
    }
}

