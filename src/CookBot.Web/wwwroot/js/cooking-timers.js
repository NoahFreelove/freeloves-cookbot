// src/CookBot.Web/wwwroot/js/cooking-timers.js
window.CookingTimers = {
    _timers: {},
    _dotNetRef: null,

    init(dotNetRef) {
        this._dotNetRef = dotNetRef;
    },

    start(timerId, durationSeconds, displayLabel) {
        const endTime = Date.now() + (durationSeconds * 1000);
        this._timers[timerId] = {
            endTime,
            displayLabel: displayLabel || null,
            interval: setInterval(() => {
                const remaining = Math.max(0, endTime - Date.now());
                const secs = Math.ceil(remaining / 1000);
                if (this._dotNetRef) {
                    this._dotNetRef.invokeMethodAsync('OnTimerTick', timerId, secs);
                }
                if (secs <= 0) {
                    const label = this._timers[timerId]?.displayLabel;
                    this.stop(timerId);
                    this._notify(timerId, label);
                    if (this._dotNetRef) {
                        this._dotNetRef.invokeMethodAsync('OnTimerComplete', timerId);
                    }
                }
            }, 1000)
        };
    },

    stop(timerId) {
        if (this._timers[timerId]) {
            clearInterval(this._timers[timerId].interval);
            delete this._timers[timerId];
        }
    },

    getRemaining(timerId) {
        const timer = this._timers[timerId];
        if (!timer) return 0;
        return Math.max(0, Math.ceil((timer.endTime - Date.now()) / 1000));
    },

    async requestNotificationPermission() {
        if ('Notification' in window && Notification.permission === 'default') {
            return await Notification.requestPermission();
        }
        return Notification.permission || 'denied';
    },

    _notify(timerId, displayLabel) {
        const label = displayLabel || `Timer ${timerId}`;
        // Audio alert
        try {
            const ctx = new (window.AudioContext || window.webkitAudioContext)();
            const osc = ctx.createOscillator();
            osc.type = 'sine';
            osc.frequency.value = 800;
            osc.connect(ctx.destination);
            osc.start();
            setTimeout(() => { osc.stop(); ctx.close(); }, 500);
        } catch (e) { /* audio not available */ }

        // Browser notification
        if ('Notification' in window && Notification.permission === 'granted') {
            new Notification('Timer complete', { body: `${label} is done.`, icon: '/favicon.ico' });
        }
    },

    dispose() {
        Object.keys(this._timers).forEach(id => this.stop(id));
        this._dotNetRef = null;
    }
};
