var kleenestar = kleenestar || {}
kleenestar.calendar = kleenestar.calendar || {}

/**
 * The conflict panel of the appointment dialog.
 *
 * The dialog plans a booking - a time, maybe a series, participants and resources - and the
 * panel says, while it is being filled in, what that booking collides with: a participant who
 * attends something else at the time, a resource booked for something else. Whenever an input
 * of the dialog changes, the inputs are posted to the conflict endpoint the panel names
 * (data-uri), together with the key of the appointment being changed (data-appointment), whose
 * own bookings do not count. The answer is drawn as a list; a resource booked twice is marked
 * as such, because saving refuses it unless the dialog books it anyway.
 */
kleenestar.calendar.ConflictsCtrl = class extends webexpress.webui.Ctrl {
    /**
     * The time the panel waits after the last change before it asks.
     */
    DELAY = 500;

    /**
     * Constructor
     * @param {HTMLElement} element - The DOM element associated with the instance.
     */
    constructor(element) {
        super(element);

        this._uri = element.dataset.uri || "";
        this._appointment = element.dataset.appointment || "";
        this._label = element.dataset.label || "";
        this._none = element.dataset.none || "";
        this._checking = element.dataset.checking || "";
        this._timer = null;
        this._sequence = 0;
        this._form = element.closest("form");

        if (!this._form || !this._uri) {
            return;
        }

        const schedule = () => this._schedule();

        this._form.addEventListener("change", schedule);
        this._form.addEventListener("input", schedule);

        // the dialog fills its inputs after it opened; the first check follows that
        document.addEventListener(webexpress.webui.Event.DATA_ARRIVED_EVENT, (event) => {
            if (!event.target || !this._form.contains(event.target) && event.target !== this._form) {
                return;
            }

            schedule();
        });

        window.setTimeout(schedule, 1200);
    }

    /**
     * Asks again once the inputs have been left alone for a moment.
     */
    _schedule() {
        window.clearTimeout(this._timer);
        this._timer = window.setTimeout(() => this._check(), this.DELAY);
    }

    /**
     * Collects the inputs of the dialog the way its form does.
     * @returns {object} The payload.
     */
    _payload() {
        const data = {};

        for (const el of Array.from(this._form.elements || [])) {
            if (!el.name || !["INPUT", "TEXTAREA", "SELECT"].includes(el.tagName) || el.type === "file") {
                continue;
            }

            const ctrl = webexpress.webui.Controller.getInstanceByElement(el);
            const value = (ctrl && typeof ctrl.value !== "undefined") ? ctrl.value : el.value;

            if (el.type === "checkbox") {
                data[el.name] = el.checked;
            } else if (el.type === "radio") {
                if (el.checked) {
                    data[el.name] = value;
                }
            } else if (el.tagName === "SELECT" && el.multiple) {
                data[el.name] = Array.from(el.selectedOptions).map((o) => o.value);
            } else {
                data[el.name] = value;
            }
        }

        data.Appointment = this._appointment;

        return data;
    }

    /**
     * Posts the inputs and draws the answer; an answer overtaken by a newer question is dropped.
     */
    async _check() {
        const sequence = ++this._sequence;

        this._draw(null, true);

        try {
            const response = await fetch(this._uri, {
                method: "POST",
                credentials: "same-origin",
                headers: { "Content-Type": "application/json; charset=utf-8", "Accept": "application/json" },
                body: JSON.stringify(this._payload())
            });

            if (sequence !== this._sequence) {
                return;
            }

            if (!response.ok) {
                this._element.innerHTML = "";
                return;
            }

            const data = await response.json();

            this._draw(data && data.checked ? (data.conflicts || []) : null, false);
        } catch {
            if (sequence === this._sequence) {
                this._element.innerHTML = "";
            }
        }
    }

    /**
     * Draws the panel.
     * @param {Array|null} conflicts - The conflicts, or null when there was nothing to check.
     * @param {boolean} busy - Whether a question is out.
     */
    _draw(conflicts, busy) {
        const esc = (text) => String(text ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", "\"": "&quot;", "'": "&#39;" }[c]));

        if (busy) {
            if (!this._element.firstChild) {
                this._element.innerHTML = `<div class="small text-secondary">${esc(this._checking)}</div>`;
            }
            return;
        }

        if (conflicts === null) {
            this._element.innerHTML = "";
            return;
        }

        if (conflicts.length === 0) {
            this._element.innerHTML = `<div class="small text-success"><i class="fas fa-check me-1"></i>${esc(this._none)}</div>`;
            return;
        }

        const rows = conflicts.map((item) => {
            const icon = item.kind === "resource" ? "fa-door-open text-danger" : "fa-user text-warning";
            const text = item.uri
                ? `<a href="${esc(item.uri)}" target="_blank" rel="noopener">${esc(item.text)}</a>`
                : esc(item.text);

            return `<li class="mb-1"><i class="fas ${icon} me-2"></i>${text}</li>`;
        }).join("");

        this._element.innerHTML = `<div class="alert alert-warning py-2 mb-0">`
            + `<div class="fw-semibold mb-1"><i class="fas fa-triangle-exclamation me-1"></i>${esc(this._label)} (${conflicts.length})</div>`
            + `<ul class="list-unstyled small mb-0">${rows}</ul></div>`;
    }
};

// register the class in the controller
webexpress.webui.Controller.registerClass("wx-kleenestar-calendar-conflicts", kleenestar.calendar.ConflictsCtrl);
