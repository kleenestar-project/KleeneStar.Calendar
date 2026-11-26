var kleenestar = kleenestar || {}
kleenestar.calendar = kleenestar.calendar || {}

/**
 * Reads the calendar file chosen in the import dialog into the dialog's content input.
 *
 * The forms of the framework post their inputs as JSON and leave a file input out, so the file
 * the dialog's chooser (name "File") holds would never reach the server. When a file is chosen,
 * its text is read in the browser and written into the content input (name "Content") - the
 * same input that takes pasted text - and the form posts that.
 */
kleenestar.calendar.Import = new class {
    /**
     * Constructor
     */
    constructor() {
        document.addEventListener("change", (event) => this.read(event.target));
    }

    /**
     * Reads a chosen file into the content input of its form.
     * @param {HTMLElement} input - The element that changed.
     */
    read(input) {
        if (!input || input.type !== "file" || input.name !== "File" || !input.files || input.files.length === 0) {
            return;
        }

        const form = input.closest("form");
        const content = form ? form.querySelector("[name=\"Content\"]") : null;

        if (!content) {
            return;
        }

        const reader = new FileReader();

        reader.onload = () => {
            content.value = String(reader.result || "");
            content.dispatchEvent(new Event("input", { bubbles: true }));
            content.dispatchEvent(new Event("change", { bubbles: true }));
        };

        reader.readAsText(input.files[0]);
    }
};
