export function localDate() {
    const now = new Date();
    const month = String(now.getMonth() + 1).padStart(2, "0");
    const day = String(now.getDate()).padStart(2, "0");
    return `${now.getFullYear()}-${month}-${day}`;
}

export function installKeyGuard() {
    const guarded = new Set(["ArrowUp", "ArrowDown", "Home", "End", " ", "Spacebar"]);
    const listener = (event) => {
        if (guarded.has(event.key) && event.target instanceof Element && event.target.closest('[role="listbox"]')) {
            event.preventDefault();
        }
    };
    document.addEventListener("keydown", listener);
    return { dispose: () => document.removeEventListener("keydown", listener) };
}

export function trapFocus(element) {
    const selector = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
    const listener = (event) => {
        if (event.key !== "Tab") {
            return;
        }
        const items = Array.from(element.querySelectorAll(selector));
        if (items.length === 0) {
            event.preventDefault();
            return;
        }
        const first = items[0];
        const last = items[items.length - 1];
        if (event.shiftKey && (document.activeElement === first || !element.contains(document.activeElement))) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && (document.activeElement === last || !element.contains(document.activeElement))) {
            event.preventDefault();
            first.focus();
        }
    };
    document.addEventListener("keydown", listener);
    return { dispose: () => document.removeEventListener("keydown", listener) };
}
