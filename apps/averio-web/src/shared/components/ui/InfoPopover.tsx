import { useEffect, useId, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

interface InfoPopoverProps {
    /** Localized subject, e.g. "Allowance remaining". */
    label: string;
    children: ReactNode;
    /** Optional ID for the explanatory text, referenced by a control’s aria-describedby. */
    id?: string;
}

/** Click/tap help in the native top layer, including inside AverioDialog. */
export function InfoPopover({ label, children, id }: InfoPopoverProps) {
    const { t } = useTranslation();
    const generatedId = useId();
    const contentId = generatedId;
    const descriptionId = id ?? generatedId + '-text';
    const trigger = useRef<HTMLButtonElement>(null);
    const panel = useRef<HTMLSpanElement>(null);
    const [open, setOpen] = useState(false);

    useEffect(() => {
        if (!open) return;
        const position = () => {
            const anchor = trigger.current;
            const popup = panel.current;
            if (!anchor || !popup) return;
            const rect = anchor.getBoundingClientRect();
            const margin = 12;
            const gap = 6;
            const viewport = window.visualViewport;
            const left = viewport?.offsetLeft ?? 0;
            const top = viewport?.offsetTop ?? 0;
            const width = viewport?.width ?? window.innerWidth;
            const height = viewport?.height ?? window.innerHeight;
            const below = top + height - rect.bottom - gap - margin;
            const above = rect.top - top - gap - margin;
            const placeBelow = below >= Math.min(popup.scrollHeight, 240) || below >= above;
            popup.style.maxHeight = Math.max(0, Math.min(height - margin * 2, placeBelow ? below : above)) + 'px';
            popup.style.maxWidth = Math.max(0, width - margin * 2) + 'px';
            popup.style.left = Math.max(left + margin, Math.min(rect.left, left + width - popup.offsetWidth - margin)) + 'px';
            const desiredTop = placeBelow ? rect.bottom + gap : rect.top - gap - popup.offsetHeight;
            popup.style.top = Math.max(top + margin, Math.min(desiredTop, top + height - popup.offsetHeight - margin)) + 'px';
        };
        position();
        const observer = new ResizeObserver(position);
        if (panel.current) observer.observe(panel.current);
        window.addEventListener('resize', position);
        window.addEventListener('scroll', position, true);
        window.visualViewport?.addEventListener('resize', position);
        window.visualViewport?.addEventListener('scroll', position);
        return () => {
            observer.disconnect();
            window.removeEventListener('resize', position);
            window.removeEventListener('scroll', position, true);
            window.visualViewport?.removeEventListener('resize', position);
            window.visualViewport?.removeEventListener('scroll', position);
        };
    }, [open]);

    return <span className="inline-flex align-middle">
        <button ref={trigger} type="button" popoverTarget={contentId}
            aria-label={t('common.infoAbout', { subject: label })}
            aria-expanded={open} aria-controls={contentId} aria-haspopup="dialog"
            className="inline-flex size-11 shrink-0 items-center justify-center rounded-full text-slate-500 hover:bg-emerald-50 hover:text-emerald-700 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:text-slate-400 dark:hover:bg-emerald-950 dark:hover:text-emerald-300">
            <svg aria-hidden="true" viewBox="0 0 20 20" className="size-4" fill="none" stroke="currentColor" strokeWidth="1.5">
                <circle cx="10" cy="10" r="8" />
                <path d="M10 9v5" strokeLinecap="round" />
                <circle cx="10" cy="6" r=".75" fill="currentColor" stroke="none" />
            </svg>
        </button>
        <span ref={panel} id={contentId} popover="auto" role="dialog" aria-label={label} aria-describedby={descriptionId} tabIndex={-1}
            onToggle={event => {
                const isOpen = event.newState === 'open';
                setOpen(isOpen);
                if (isOpen) panel.current?.focus({ preventScroll: true });
            }}
            onKeyDown={event => {
                if (event.key === 'Escape') {
                    event.preventDefault();
                    event.stopPropagation();
                    panel.current?.hidePopover();
                    trigger.current?.focus({ preventScroll: true });
                }
            }}
            className="fixed inset-auto m-0 w-80 max-w-[calc(100vw-24px)] overflow-y-auto overscroll-contain rounded-xl border border-l-[3px] border-[#E4E7EC] border-l-primary-500 bg-white p-4 text-left text-sm leading-relaxed font-normal tracking-normal whitespace-normal text-[#667085] normal-case shadow-lg outline-none dark:border-[#233044] dark:border-l-primary-500 dark:bg-[#0D1726] dark:text-[#AAB4C4]">
            <span id={descriptionId}>{children}</span>
        </span>
    </span>;
}
