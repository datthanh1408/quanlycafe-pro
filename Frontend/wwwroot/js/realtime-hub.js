/* ==========================================================================
   REALTIME HUB CLIENT (SignalR + Tự động Fallback Polling 15s theo Mục 3.1 & 6.10)
   ========================================================================== */
(function () {
    const callbacks = {
        onNewOrder: [],
        onStatusChanged: [],
        onTableChanged: []
    };

    let connection = null;
    let fallbackTimer = null;
    let lastKnownOrderMarker = null;

    window.DatMocRealtime = {
        onNewOrder: (fn) => callbacks.onNewOrder.push(fn),
        onStatusChanged: (fn) => callbacks.onStatusChanged.push(fn),
        onTableChanged: (fn) => callbacks.onTableChanged.push(fn),
        joinOrderGroup: (orderCode) => {
            if (connection && connection.state === "Connected" && orderCode) {
                connection.invoke("JoinOrderGroup", orderCode).catch(() => {});
            }
        },
        showToast: function (title, message, variant = "primary") {
            const container = document.getElementById("globalToastContainer");
            if (!container) return;
            const bgMap = {
                success: "bg-success text-white",
                danger: "bg-danger text-white",
                warning: "bg-warning text-dark",
                primary: "bg-coffee text-white"
            };
            const cls = bgMap[variant] || bgMap.primary;
            const el = document.createElement("div");
            el.className = `toast align-items-center border-0 show mb-2 shadow ${cls}`;
            el.setAttribute("role", "alert");
            el.innerHTML = `
                <div class="d-flex">
                    <div class="toast-body">
                        <strong class="d-block">${title}</strong>
                        <span>${message}</span>
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" aria-label="Đóng"></button>
                </div>`;
            el.querySelector(".btn-close")?.addEventListener("click", () => el.remove());
            container.appendChild(el);
            setTimeout(() => el.remove(), 6000);
        }
    };

    function startPollingFallback() {
        if (fallbackTimer) return;
        fallbackTimer = setInterval(async () => {
            try {
                const res = await fetch("/api/realtime/ping", { headers: { "Accept": "application/json" } });
                if (!res.ok) return;
                const data = await res.json();
                if (lastKnownOrderMarker !== null && data.marker !== lastKnownOrderMarker) {
                    callbacks.onTableChanged.forEach(fn => fn({ polled: true }));
                    callbacks.onStatusChanged.forEach(fn => fn({ polled: true, pendingCount: data.pendingCount }));
                }
                lastKnownOrderMarker = data.marker;
                updateStaffPendingBadge(data.pendingCount);
            } catch (_) { /* ignore offline */ }
        }, 15000);
    }

    function stopPollingFallback() {
        if (fallbackTimer) {
            clearInterval(fallbackTimer);
            fallbackTimer = null;
        }
    }

    function updateStaffPendingBadge(count) {
        if (typeof count !== "number") return;
        document.querySelectorAll("[data-staff-pending-badge]").forEach(el => {
            el.textContent = count;
            el.style.display = count > 0 ? "inline-block" : "none";
        });
    }

    function playGentleChime() {
        try {
            const AudioCtx = window.AudioContext || window.webkitAudioContext;
            if (!AudioCtx) return;
            const ctx = new AudioCtx();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();
            osc.type = "sine";
            osc.frequency.setValueAtTime(660, ctx.currentTime);
            osc.frequency.setValueAtTime(880, ctx.currentTime + 0.12);
            gain.gain.setValueAtTime(0.08, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.38);
            osc.connect(gain);
            gain.connect(ctx.destination);
            osc.start();
            osc.stop(ctx.currentTime + 0.4);
        } catch (_) {}
    }

    document.addEventListener("DOMContentLoaded", () => {
        if (typeof signalR === "undefined") {
            startPollingFallback();
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/order")
            .withAutomaticReconnect([0, 2000, 5000, 10000])
            .build();

        connection.on("NewOrderReceived", (payload) => {
            playGentleChime();
            window.DatMocRealtime.showToast(
                `Đơn mới: ${payload.code}`,
                `${payload.customerName} • ${payload.orderType} • ${payload.totalFormatted}`,
                "success"
            );
            document.querySelectorAll("[data-staff-pending-badge]").forEach(el => {
                const cur = parseInt(el.textContent || "0", 10) || 0;
                el.textContent = cur + 1;
                el.style.display = "inline-block";
            });
            callbacks.onNewOrder.forEach(fn => fn(payload));
        });

        connection.on("OrderStatusChanged", (payload) => {
            callbacks.onStatusChanged.forEach(fn => fn(payload));
        });

        connection.on("TableStatusChanged", (payload) => {
            callbacks.onTableChanged.forEach(fn => fn(payload));
        });

        connection.onreconnecting(() => startPollingFallback());
        connection.onreconnected(() => stopPollingFallback());
        connection.onclose(() => startPollingFallback());

        connection.start()
            .then(() => {
                stopPollingFallback();
                const watchCode = document.body.dataset.watchOrderCode;
                if (watchCode) {
                    connection.invoke("JoinOrderGroup", watchCode).catch(() => {});
                }
            })
            .catch(() => startPollingFallback());
    });
})();

