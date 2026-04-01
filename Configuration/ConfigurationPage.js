/* global ApiClient */
"use strict";

(function () {
    const API_BASE = "SeerrRequestFav";

    // ── Helpers ────────────────────────────────────────────────────────────────────

    function el(id) { return document.getElementById(id); }

    function setBanner(text, type /* 'success'|'error'|'info' */) {
        const banner = el("srfStatusBanner");
        if (!banner) return;
        banner.textContent = text;
        banner.className = `srfStatusBanner ${type}`;
        banner.style.display = "block";
        if (type === "success") {
            setTimeout(() => { banner.style.display = "none"; }, 6000);
        }
    }

    function setInlineResult(spanId, text, success) {
        const span = el(spanId);
        if (!span) return;
        span.textContent = text;
        span.style.color = success ? "#00a450" : "#dc3232";
    }

    async function apiGet(path) {
        const url = ApiClient.getUrl(`/${API_BASE}/${path}`);
        const resp = await fetch(url, {
            headers: { "X-MediaBrowser-Token": ApiClient.accessToken() }
        });
        if (!resp.ok) throw new Error(`HTTP ${resp.status}`);
        return resp.json();
    }

    async function apiPost(path, body) {
        const url = ApiClient.getUrl(`/${API_BASE}/${path}`);
        const resp = await fetch(url, {
            method: "POST",
            headers: {
                "Content-Type": "application/json",
                "X-MediaBrowser-Token": ApiClient.accessToken()
            },
            body: JSON.stringify(body ?? {})
        });
        const data = await resp.json().catch(() => ({}));
        if (!resp.ok) throw Object.assign(new Error(data.message || `HTTP ${resp.status}`), { data, status: resp.status });
        return data;
    }

    // ── Load config ────────────────────────────────────────────────────────────────

    async function loadConfig() {
        try {
            const cfg = await apiGet("PluginConfiguration");

            setCheck("srfIsEnabled",                    cfg.IsEnabled);
            setInput("srfJellyseerrUrl",                cfg.JellyseerrUrl);
            setInput("srfApiKey",                       cfg.ApiKey);
            setCheck("srfResponsiveFavoriteRequests",   cfg.ResponsiveFavoriteRequests);
            setCheck("srfRemoveRequestedFromFavorites", cfg.RemoveRequestedFromFavorites);
            setCheck("srfRequestFirstSeason",           cfg.RequestFirstSeason);
            setInput("srfTaskTimeoutMinutes",           cfg.TaskTimeoutMinutes);
            setInput("srfRequestTimeout",               cfg.RequestTimeout);
            setInput("srfRetryAttempts",                cfg.RetryAttempts);
            setCheck("srfEnableDebugLogging",           cfg.EnableDebugLogging);
            setCheck("srfEnableTraceLogging",           cfg.EnableTraceLogging);

            if (cfg.PluginVersion) {
                const v = el("srfPluginVersion");
                if (v) v.textContent = `SeerrRequestFav v${cfg.PluginVersion}`;
            }
        } catch (err) {
            setBanner(`Failed to load configuration: ${err.message}`, "error");
        }
    }

    function setInput(id, value) {
        const inp = el(id);
        if (inp && value !== undefined && value !== null) inp.value = value;
    }

    function setCheck(id, value) {
        const inp = el(id);
        if (inp) inp.checked = !!value;
    }

    // ── Save config ────────────────────────────────────────────────────────────────

    async function saveConfig() {
        const payload = {
            IsEnabled:                    el("srfIsEnabled")?.checked,
            JellyseerrUrl:               el("srfJellyseerrUrl")?.value?.trim(),
            ApiKey:                       el("srfApiKey")?.value?.trim(),
            ResponsiveFavoriteRequests:   el("srfResponsiveFavoriteRequests")?.checked,
            RemoveRequestedFromFavorites: el("srfRemoveRequestedFromFavorites")?.checked,
            RequestFirstSeason:           el("srfRequestFirstSeason")?.checked,
            TaskTimeoutMinutes:           parseFloatOrNull("srfTaskTimeoutMinutes"),
            RequestTimeout:               parseIntOrNull("srfRequestTimeout"),
            RetryAttempts:                parseIntOrNull("srfRetryAttempts"),
            EnableDebugLogging:           el("srfEnableDebugLogging")?.checked,
            EnableTraceLogging:           el("srfEnableTraceLogging")?.checked
        };

        try {
            await apiPost("PluginConfiguration", payload);
            setBanner("Settings saved successfully.", "success");
        } catch (err) {
            setBanner(`Save failed: ${err.message}`, "error");
        }
    }

    function parseIntOrNull(id) {
        const v = parseInt(el(id)?.value);
        return isNaN(v) ? null : v;
    }
    function parseFloatOrNull(id) {
        const v = parseFloat(el(id)?.value);
        return isNaN(v) ? null : v;
    }

    // ── Test connection ────────────────────────────────────────────────────────────

    async function testConnection() {
        const btn = el("srfTestConnectionBtn");
        setInlineResult("srfTestConnectionResult", "Testing…", true);
        if (btn) btn.disabled = true;

        try {
            const result = await apiPost("TestConnection", {
                JellyseerrUrl: el("srfJellyseerrUrl")?.value?.trim(),
                ApiKey:        el("srfApiKey")?.value?.trim()
            });
            setInlineResult(
                "srfTestConnectionResult",
                result.message || "Connection successful",
                true
            );
        } catch (err) {
            setInlineResult(
                "srfTestConnectionResult",
                err.data?.message || err.message || "Connection failed",
                false
            );
        } finally {
            if (btn) btn.disabled = false;
        }
    }

    // ── Sync favourites ────────────────────────────────────────────────────────────

    async function syncFavorites() {
        const btn = el("srfSyncFavoritesBtn");
        setInlineResult("srfSyncResult", "Syncing…", true);
        if (btn) btn.disabled = true;

        try {
            const result = await apiPost("SyncFavorites");
            setInlineResult("srfSyncResult",
                result.message || `Done. Created: ${result.created ?? 0}`, true);
        } catch (err) {
            setInlineResult("srfSyncResult",
                err.data?.message || err.message || "Sync failed", false);
        } finally {
            if (btn) btn.disabled = false;
        }
    }

    // ── Bootstrap ─────────────────────────────────────────────────────────────────

    function init(page) {
        loadConfig();

        page.querySelector("#srfSaveConfigBtn")
            ?.addEventListener("click", saveConfig);

        page.querySelector("#srfTestConnectionBtn")
            ?.addEventListener("click", testConnection);

        page.querySelector("#srfSyncFavoritesBtn")
            ?.addEventListener("click", syncFavorites);
    }

    // Jellyfin injects the page node as the argument when the script is loaded.
    window.SeerrRequestFavConfigPage = { init };

    document.addEventListener("DOMContentLoaded", () => {
        const page = document.getElementById("seerrRequestFavConfigPage");
        if (page) init(page);
    });
})();
