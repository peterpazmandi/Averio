import { useEffect, useId, useRef } from 'react';
import type { ReactNode, RefObject } from 'react';
import { createPortal } from 'react-dom';
import { ConceptIcon } from '../concept/ConceptIcon';
import { AverioButton } from './AverioButton';

let openDialogCount = 0;
let originalBodyOverflow = '';

interface AverioDialogProps {
    open: boolean;
    title: string;
    closeLabel: string;
    children: ReactNode;
    actions: ReactNode;
    onDismiss: () => void;
    busy?: boolean;
    wide?: boolean;
    extraWide?: boolean;
    initialFocusRef?: RefObject<HTMLElement | null>;
}

/** Modal dialog with a content-sized bottom sheet on mobile. Dismissal never accepts an action. */
export function AverioDialog({ open, title, closeLabel, children, actions, onDismiss, busy = false, wide = false, extraWide = false, initialFocusRef }: AverioDialogProps) {
    const dialogRef = useRef<HTMLDialogElement>(null);
    const titleId = useId();
    useEffect(() => {
        const dialog = dialogRef.current;
        if (!open || !dialog) return;
        const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        dialog.showModal();
        if (openDialogCount++ === 0) {
            originalBodyOverflow = document.body.style.overflow;
            document.body.style.overflow = 'hidden';
        }
        initialFocusRef?.current?.focus();
        return () => {
            dialog.close();
            if (--openDialogCount === 0) document.body.style.overflow = originalBodyOverflow;
            if (previousFocus?.isConnected) previousFocus.focus();
        };
    }, [open, initialFocusRef]);

    return createPortal(
        <dialog ref={dialogRef} className={(extraWide ? "min-[601px]:w-[min(1400px,calc(100vw-48px))] " : wide ? "min-[601px]:w-[min(900px,calc(100vw-48px))] " : "min-[601px]:w-[min(560px,calc(100vw-48px))] ") + "fixed inset-x-0 top-auto bottom-0 m-0 h-fit max-h-dvh w-full max-w-none min-w-0 overflow-hidden rounded-t-[20px] border border-[#E4E7EC] bg-white p-0 text-[#152033] shadow-[0_24px_80px_rgba(0,0,0,0.25)] backdrop:bg-[#030A1699] open:flex open:flex-col dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#F3F6FA] min-[601px]:inset-0 min-[601px]:m-auto min-[601px]:max-h-[calc(100dvh-48px)] min-[601px]:rounded-[20px]"} aria-labelledby={titleId} aria-busy={busy}
            onKeyDown={event => {
                event.stopPropagation();
                if (event.key !== 'Tab') return;
                const controls = [...event.currentTarget.querySelectorAll<HTMLElement>(
                    'button, a[href], input, select, textarea, [tabindex]',
                )].filter(element => element.tabIndex >= 0 && !element.matches(':disabled') && element.getClientRects().length > 0);
                const first = controls[0];
                const last = controls[controls.length - 1];
                if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault(); last?.focus();
                } else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault(); first?.focus();
                }
            }}
            onCancel={event => { event.stopPropagation(); event.preventDefault(); if (!busy) onDismiss(); }}>
            <header className="flex shrink-0 items-center justify-between gap-4 border-b border-[#E4E7EC] px-5 py-4 dark:border-[#233044] min-[601px]:px-6">
                <h2 id={titleId} className="m-0 text-xl leading-snug font-bold wrap-anywhere">{title}</h2>
                <AverioButton size="icon" disabled={busy} aria-label={closeLabel} onClick={onDismiss}>
                    <ConceptIcon name="close" />
                </AverioButton>
            </header>
            <div className="min-h-0 overflow-y-auto overscroll-contain p-5 text-sm leading-relaxed text-[#667085] dark:text-[#AAB4C4] min-[601px]:p-6">{children}</div>
            <footer className="flex shrink-0 flex-wrap justify-end gap-3 border-t border-[#E4E7EC] px-5 pt-4 pb-[calc(20px+env(safe-area-inset-bottom))] dark:border-[#233044] max-[600px]:[&>button]:flex-auto min-[601px]:px-6">{actions}</footer>
        </dialog>, document.body,
    );
}

