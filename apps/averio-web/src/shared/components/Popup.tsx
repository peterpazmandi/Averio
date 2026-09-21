import { useEffect, useId, useRef } from 'react';
import type { ReactNode, RefObject } from 'react';
import { createPortal } from 'react-dom';
import { ConceptIcon } from './concept/ConceptIcon';
import '../styles/concept.css';
import './Popup.css';

interface PopupProps {
    open: boolean;
    title: string;
    closeLabel: string;
    children: ReactNode;
    actions: ReactNode;
    onDismiss: () => void;
    initialFocusRef?: RefObject<HTMLElement | null>;
}

/** Modal dialog with a content-sized bottom sheet on mobile. Dismissal never accepts an action. */
export function Popup({ open, title, closeLabel, children, actions, onDismiss, initialFocusRef }: PopupProps) {
    const dialogRef = useRef<HTMLDialogElement>(null);
    const titleId = useId();
    useEffect(() => {
        const dialog = dialogRef.current;
        if (!open || !dialog) return;
        const previousFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
        const previousOverflow = document.body.style.overflow;
        dialog.showModal();
        document.body.style.overflow = 'hidden';
        initialFocusRef?.current?.focus();
        return () => {
            dialog.close();
            document.body.style.overflow = previousOverflow;
            if (previousFocus?.isConnected) previousFocus.focus();
        };
    }, [open, initialFocusRef]);

    return createPortal(
        <dialog ref={dialogRef} className="av-ui av-popup" aria-labelledby={titleId}
            onKeyDown={event => {
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
            onCancel={event => { event.preventDefault(); onDismiss(); }}>
            <header className="av-popup-header">
                <h2 id={titleId}>{title}</h2>
                <button type="button" className="av-btn av-popup-close" aria-label={closeLabel} onClick={onDismiss}>
                    <ConceptIcon name="close" />
                </button>
            </header>
            <div className="av-popup-content">{children}</div>
            <footer className="av-popup-actions">{actions}</footer>
        </dialog>, document.body,
    );
}
