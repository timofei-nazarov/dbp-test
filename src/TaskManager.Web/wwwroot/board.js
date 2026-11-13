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

const mobileQuery = "(max-width: 639.98px)";

export function isMobileViewport() {
    return window.matchMedia(mobileQuery).matches;
}

export function observeViewport(ref) {
    const query = window.matchMedia(mobileQuery);
    const listener = (event) => ref.invokeMethodAsync("ViewportChanged", event.matches);
    query.addEventListener("change", listener);
    return { dispose: () => query.removeEventListener("change", listener) };
}

export function observeColumns(scroller, ref) {
    let last = "";
    const report = () => {
        const bounds = scroller.getBoundingClientRect();
        const columns = Array.from(scroller.querySelectorAll(".column"));
        let visibleCount = 0;
        for (const column of columns) {
            const box = column.getBoundingClientRect();
            const visible = Math.min(box.right, bounds.right) - Math.max(box.left, bounds.left);
            if (box.width > 0 && visible >= box.width / 2) {
                visibleCount++;
            }
        }
        const overflowing = scroller.scrollWidth > scroller.clientWidth + 1;
        const left = overflowing && scroller.scrollLeft > 1;
        const right = overflowing && scroller.scrollLeft + scroller.clientWidth < scroller.scrollWidth - 1;
        const key = `${left}|${right}|${visibleCount}|${overflowing}`;
        if (key !== last) {
            last = key;
            ref.invokeMethodAsync("ColumnsChanged", left, right, visibleCount, overflowing);
        }
    };
    scroller.addEventListener("scroll", report, { passive: true });
    const observer = new ResizeObserver(report);
    observer.observe(scroller);
    report();
    return {
        dispose: () => {
            scroller.removeEventListener("scroll", report);
            observer.disconnect();
        }
    };
}

export function positionPopup(popup, anchor, width) {
    const edge = 8;
    const gap = 4;
    const box = anchor.getBoundingClientRect();
    popup.style.position = "fixed";
    popup.style.width = `${width}px`;
    popup.style.left = `${Math.max(edge, box.right - width)}px`;
    const height = popup.offsetHeight;
    const below = box.bottom + gap;
    const fitsBelow = below + height <= window.innerHeight - edge;
    const above = box.top - gap - height;
    popup.style.top = `${fitsBelow || above < edge ? below : above}px`;
    popup.style.visibility = "visible";
}

export function watchDismiss(popup, ref) {
    const onScroll = (event) => {
        if (event.target instanceof Node && popup.contains(event.target)) {
            return;
        }
        ref.invokeMethodAsync("DismissMenu");
    };
    const onResize = () => ref.invokeMethodAsync("DismissMenu");
    window.addEventListener("scroll", onScroll, true);
    window.addEventListener("resize", onResize);
    return {
        dispose: () => {
            window.removeEventListener("scroll", onScroll, true);
            window.removeEventListener("resize", onResize);
        }
    };
}

export function lockScroll() {
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return { dispose: () => { document.body.style.overflow = previous; } };
}

export function watchPopover(panel, button, ref) {
    const onKey = (event) => {
        if (event.key === "Escape") {
            ref.invokeMethodAsync("ClosePanel", true);
        }
    };
    const onPointer = (event) => {
        if (event.target instanceof Node && !panel.contains(event.target) && !button.contains(event.target)) {
            ref.invokeMethodAsync("ClosePanel", false);
        }
    };
    document.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onPointer);
    return {
        dispose: () => {
            document.removeEventListener("keydown", onKey);
            document.removeEventListener("pointerdown", onPointer);
        }
    };
}
