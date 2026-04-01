/* global ApiClient */

const API_BASE = "SeerrRequestFav";

// ── Helpers ────────────────────────────────────────────────────────────────────

function el(page, id) { return page.querySelector(`#${id}`); }

function setBanner(page, text, type /* 'success'|'error'|'info' */) {
    const banner = el(page, "srfStatusBanner");
    if (!banner) return;
    banner.textContent = text;
    banner.className = `srfStatusBanner ${type}`;
    banner.style.display = "block";
    if (type === "success") {
        setTimeout(() => { banner.style.display = "none"; }, 6000);
    }
}

function setInlineResult(page, spanId, text, success) {
    const span = el(page, spanId);
    if (!span) return;
    span.textContent = text;
    span.style.color = success ? "#00a450" : "#dc3232";
}

/** Extracts a human-readable message from an ApiClient.ajax rejection. */
function ajaxErrMsg(err) {
    if (err && err.responseJSON && err.responseJSON.message) return err.responseJSON.message;
    if (err && err.responseText) {
        try { const j = JSON.parse(err.responseText); if (j.message) return j.message; } catch (_) {}
        return err.responseText;
    }
    if (err && err.statusText) return `HTTP ${err.status}: ${err.statusText}`;
    if (err instanceof Error) return err.message;
    return String(err);
}

function apiGet(path) {
    return ApiClient.ajax({
        url: ApiClient.getUrl(`${API_BASE}/${path}`),
        type: "GET",
        dataType: "json"
    });
}

function apiPost(path, body) {
    return ApiClient.ajax({
        url: ApiClient.getUrl(`${API_BASE}/${path}`),
        type: "POST",
        data: JSON.stringify(body ?? {}),
        contentType: "application/json",
        dataType: "json"
    });
}

// ── Load config ────────────────────────────────────────────────────────────────

async function loadConfig(page) {
    try {
        const cfg = await apiGet("PluginConfiguration").catch(err => { throw new Error(ajaxErrMsg(err)); });

        setCheck(page, "srfIsEnabled",                    cfg.IsEnabled);
        setInput(page, "srfJellyseerrUrl",                cfg.JellyseerrUrl);
        setInput(page, "srfApiKey",                       cfg.ApiKey);
        setCheck(page, "srfResponsiveFavoriteRequests",   cfg.ResponsiveFavoriteRequests);
        setCheck(page, "srfRemoveRequestedFromFavorites", cfg.RemoveRequestedFromFavorites);
        setCheck(page, "srfRequestFirstSeason",           cfg.RequestFirstSeason);
        setInput(page, "srfTaskTimeoutMinutes",           cfg.TaskTimeoutMinutes);
        setInput(page, "srfRequestTimeout",               cfg.RequestTimeout);
        setInput(page, "srfRetryAttempts",                cfg.RetryAttempts);
        setCheck(page, "srfEnableDebugLogging",           cfg.EnableDebugLogging);
        setCheck(page, "srfEnableTraceLogging",           cfg.EnableTraceLogging);

        if (cfg.PluginVersion) {
            const v = el(page, "srfPluginVersion");
            if (v) v.textContent = `SeerrRequestFav v${cfg.PluginVersion}`;
        }
    } catch (err) {
        setBanner(page, `Failed to load configuration: ${err.message}`, "error");
    }
}

function setInput(page, id, value) {
    const inp = el(page, id);
    if (inp && value !== undefined && value !== null) inp.value = value;
}

function setCheck(page, id, value) {
    const inp = el(page, id);
    if (inp) inp.checked = !!value;
}

// ── Save config ────────────────────────────────────────────────────────────────

async function saveConfig(page) {
    const payload = {
        IsEnabled:                    el(page, "srfIsEnabled")?.checked,
        JellyseerrUrl:                el(page, "srfJellyseerrUrl")?.value?.trim(),
        ApiKey:                       el(page, "srfApiKey")?.value?.trim(),
        ResponsiveFavoriteRequests:   el(page, "srfResponsiveFavoriteRequests")?.checked,
        RemoveRequestedFromFavorites: el(page, "srfRemoveRequestedFromFavorites")?.checked,
        RequestFirstSeason:           el(page, "srfRequestFirstSeason")?.checked,
        TaskTimeoutMinutes:           parseFloatOrNull(page, "srfTaskTimeoutMinutes"),
        RequestTimeout:               parseIntOrNull(page, "srfRequestTimeout"),
        RetryAttempts:                parseIntOrNull(page, "srfRetryAttempts"),
        EnableDebugLogging:           el(page, "srfEnableDebugLogging")?.checked,
        EnableTraceLogging:           el(page, "srfEnableTraceLogging")?.checked
    };

    try {
        await apiPost("PluginConfiguration", payload);
        setBanner(page, "Settings saved successfully.", "success");
    } catch (err) {
        setBanner(page, `Save failed: ${ajaxErrMsg(err)}`, "error");
    }
}

function parseIntOrNull(page, id) {
    const v = parseInt(el(page, id)?.value);
    return isNaN(v) ? null : v;
}
function parseFloatOrNull(page, id) {
    const v = parseFloat(el(page, id)?.value);
    return isNaN(v) ? null : v;
}

// ── Test connection ────────────────────────────────────────────────────────────

async function testConnection(page) {
    const btn = el(page, "srfTestConnectionBtn");
    setInlineResult(page, "srfTestConnectionResult", "Testing…", true);
    if (btn) btn.disabled = true;

    try {
        const result = await apiPost("TestConnection", {
            JellyseerrUrl: el(page, "srfJellyseerrUrl")?.value?.trim(),
            ApiKey:        el(page, "srfApiKey")?.value?.trim()
        });
        setInlineResult(
            page,
            "srfTestConnectionResult",
            result.message || "Connection successful",
            true
        );
    } catch (err) {
        setInlineResult(
            page,
            "srfTestConnectionResult",
            ajaxErrMsg(err),
            false
        );
    } finally {
        if (btn) btn.disabled = false;
    }
}

// ── Sync favourites ────────────────────────────────────────────────────────────

async function syncFavorites(page) {
    const btn = el(page, "srfSyncFavoritesBtn");
    setInlineResult(page, "srfSyncResult", "Syncing…", true);
    if (btn) btn.disabled = true;

    try {
        const result = await apiPost("SyncFavorites");
        setInlineResult(page, "srfSyncResult",
            result.message || `Done. Created: ${result.created ?? 0}`, true);
    } catch (err) {
        setInlineResult(page, "srfSyncResult", ajaxErrMsg(err), false);
    } finally {
        if (btn) btn.disabled = false;
    }
}

// ── Bootstrap ─────────────────────────────────────────────────────────────────

export default function (view) {
    let initialized = false;

    view.addEventListener("viewshow", function () {
        // Register click handlers once, guarded by initialized flag to prevent
        // duplicates if viewshow fires multiple times.
        if (!initialized) {
            view.querySelector("#srfSaveConfigBtn")
                ?.addEventListener("click", () => saveConfig(view));

            view.querySelector("#srfTestConnectionBtn")
                ?.addEventListener("click", () => testConnection(view));

            view.querySelector("#srfSyncFavoritesBtn")
                ?.addEventListener("click", () => syncFavorites(view));

            initialized = true;
        }

        loadConfig(view);
    });
}
