import {
    type CSSProperties,
    useEffect,
    useId,
    useMemo,
    useRef,
    useState,
} from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';
import { ConceptIcon } from '../../../shared/components/concept/ConceptIcon';
import { getCategoryDisplayName } from '../categoryLocalization';
import type { CategoryDto } from '../api/transactionApi';

interface Props {
    categories: CategoryDto[];
    value: string;
    onChange: (categoryId: string) => void;
    disabled?: boolean;
    label: string;
}

interface PickerPosition {
    left: number;
    width: number;
    maxHeight: number;
    placement: 'top' | 'bottom';
    top?: number;
    bottom?: number;
}

function normalize(value: string) {
    return value
        .normalize('NFD')
        .replace(/\p{M}/gu, '')
        .toLocaleLowerCase()
        .trim();
}

export function CategoryPicker({
    categories,
    value,
    onChange,
    disabled = false,
    label,
}: Props) {
    const { t } = useTranslation();
    const root = useRef<HTMLDivElement>(null);
    const panel = useRef<HTMLDivElement>(null);
    const searchInput = useRef<HTMLInputElement>(null);
    const panelId = useId();

    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');
    const [position, setPosition] = useState<PickerPosition | null>(null);

    const byId = useMemo(
        () => new Map(categories.map(category => [category.id, category])),
        [categories],
    );

    const selected = byId.get(value);
    const selectedParent = selected?.parentId
        ? byId.get(selected.parentId)
        : undefined;

    const groups = useMemo(() => {
        const term = normalize(search);

        const roots = categories
            .filter(category => category.parentId === null)
            .sort((a, b) => a.sortOrder - b.sortOrder);

        return roots.flatMap(parent => {
            const parentName = getCategoryDisplayName(parent, t);

            const children = categories
                .filter(category => category.parentId === parent.id)
                .sort((a, b) => a.sortOrder - b.sortOrder);

            if (!term) {
                return [{ parent, parentName, children }];
            }

            const parentMatches = normalize(
                `${parentName} ${parent.systemKey ?? ''}`,
            ).includes(term);

            const matchingChildren = children.filter(child => {
                const childName = getCategoryDisplayName(child, t);

                return normalize(
                    `${childName} ${child.systemKey ?? ''} ${parentName}`,
                ).includes(term);
            });

            if (!parentMatches && matchingChildren.length === 0) {
                return [];
            }

            return [{
                parent,
                parentName,
                children: parentMatches ? children : matchingChildren,
            }];
        });
    }, [categories, search, t]);

    useEffect(() => {
        if (!open) {
            setPosition(null);
            return;
        }

        function updatePosition() {
            const trigger = root.current;

            if (!trigger) return;

            const rect = trigger.getBoundingClientRect();
            const viewportPadding = 12;
            const gap = 8;
            const viewportWidth = window.innerWidth;
            const viewportHeight = window.innerHeight;

            const desiredWidth = Math.min(
                680,
                viewportWidth - viewportPadding * 2,
            );

            const width = Math.max(
                Math.min(rect.width, desiredWidth),
                Math.min(640, desiredWidth),
            );

            const centeredLeft = rect.left + rect.width / 2 - width / 2;
            const left = Math.min(
                Math.max(viewportPadding, centeredLeft),
                Math.max(
                    viewportPadding,
                    viewportWidth - width - viewportPadding,
                ),
            );

            const availableBelow =
                viewportHeight - rect.bottom - gap - viewportPadding;
            const availableAbove =
                rect.top - gap - viewportPadding;

            const useBottom =
                availableBelow >= 280
                || availableBelow >= availableAbove;

            const maxHeight = Math.max(
                180,
                Math.min(
                    560,
                    useBottom ? availableBelow : availableAbove,
                ),
            );

            setPosition(useBottom
                ? {
                    left,
                    width,
                    maxHeight,
                    placement: 'bottom',
                    top: rect.bottom + gap,
                }
                : {
                    left,
                    width,
                    maxHeight,
                    placement: 'top',
                    bottom: viewportHeight - rect.top + gap,
                });
        }

        function onPointerDown(event: PointerEvent) {
            const target = event.target as Node;

            if (
                !root.current?.contains(target)
                && !panel.current?.contains(target)
            ) {
                setOpen(false);
                setSearch('');
            }
        }

        function onKeyDown(event: KeyboardEvent) {
            if (event.key === 'Escape') {
                setOpen(false);
                setSearch('');
            }
        }

        updatePosition();

        document.addEventListener('pointerdown', onPointerDown);
        document.addEventListener('keydown', onKeyDown);
        document.addEventListener('scroll', updatePosition, true);
        window.addEventListener('resize', updatePosition);

        const frame = requestAnimationFrame(
            () => searchInput.current?.focus(),
        );

        return () => {
            cancelAnimationFrame(frame);
            document.removeEventListener('pointerdown', onPointerDown);
            document.removeEventListener('keydown', onKeyDown);
            document.removeEventListener('scroll', updatePosition, true);
            window.removeEventListener('resize', updatePosition);
        };
    }, [open]);

    function choose(categoryId: string) {
        onChange(categoryId);
        setOpen(false);
        setSearch('');
    }

    const selectedDisplayName = selected
        ? getCategoryDisplayName(selected, t)
        : t('transactionManagement.selectCategory');

    const selectedParentName = selectedParent
        ? getCategoryDisplayName(selectedParent, t)
        : null;

    const panelStyle = position
        ? {
            '--picker-left': `${position.left}px`,
            '--picker-width': `${position.width}px`,
            '--picker-max-height': `${position.maxHeight}px`,
            '--picker-top': position.top === undefined
                ? 'auto'
                : `${position.top}px`,
            '--picker-bottom': position.bottom === undefined
                ? 'auto'
                : `${position.bottom}px`,
        } as CSSProperties
        : undefined;

    const pickerPanel = open && position
        ? createPortal(
            <div
                ref={panel}
                id={panelId}
                className="av-category-picker-panel"
                data-placement={position.placement}
                role="dialog"
                aria-label={label}
                style={panelStyle}
            >
                <div className="av-category-picker-search">
                    <ConceptIcon name="search" />
                    <label
                        className="sr-only"
                        htmlFor={`${panelId}-search`}
                    >
                        {t('transactionManagement.categorySearch')}
                    </label>
                    <input
                        ref={searchInput}
                        id={`${panelId}-search`}
                        type="search"
                        value={search}
                        autoComplete="off"
                        placeholder={t(
                            'transactionManagement.categorySearchPlaceholder',
                        )}
                        onChange={event => setSearch(event.target.value)}
                    />
                    {search && (
                        <button
                            type="button"
                            className="av-category-picker-clear"
                            aria-label={t(
                                'transactionManagement.clearSearch',
                            )}
                            onClick={() => {
                                setSearch('');
                                searchInput.current?.focus();
                            }}
                        >
                            <ConceptIcon name="close" />
                        </button>
                    )}
                </div>

                <p className="av-category-picker-help">
                    {t('transactionManagement.categoryPickerHelp')}
                </p>

                {groups.length === 0 ? (
                    <div className="av-category-picker-empty">
                        <ConceptIcon name="search" />
                        <span>
                            {t('transactionManagement.categoryNoResults')}
                        </span>
                    </div>
                ) : (
                    <div className="av-category-bento">
                        {groups.map(({
                            parent,
                            parentName,
                            children,
                        }) => (
                            <section
                                className="av-category-group"
                                key={parent.id}
                                style={{
                                    '--category-accent':
                                        parent.accentColor,
                                } as CSSProperties}
                            >
                                <button
                                    type="button"
                                    className={
                                        'av-category-parent'
                                        + (value === parent.id
                                            ? ' selected'
                                            : '')
                                    }
                                    onClick={() => choose(parent.id)}
                                    aria-pressed={value === parent.id}
                                >
                                    <span
                                        className="av-category-parent-icon"
                                        aria-hidden="true"
                                    >
                                        {parent.icon}
                                    </span>
                                    <span>
                                        <strong>{parentName}</strong>
                                    </span>
                                    {value === parent.id && (
                                        <span
                                            className="av-category-selected-mark"
                                            aria-hidden="true"
                                        >
                                            ✓
                                        </span>
                                    )}
                                </button>

                                {children.length > 0 && (
                                    <div className="av-category-children">
                                        {children.map(child => (
                                            <button
                                                type="button"
                                                key={child.id}
                                                className={
                                                    'av-category-child'
                                                    + (value === child.id
                                                        ? ' selected'
                                                        : '')
                                                }
                                                onClick={() =>
                                                    choose(child.id)}
                                                aria-pressed={value === child.id}
                                            >
                                                <span aria-hidden="true">
                                                    {child.icon}
                                                </span>
                                                <span>
                                                    {getCategoryDisplayName(
                                                        child,
                                                        t,
                                                    )}
                                                </span>
                                                {value === child.id && (
                                                    <span
                                                        className="av-category-selected-mark"
                                                        aria-hidden="true"
                                                    >
                                                        ✓
                                                    </span>
                                                )}
                                            </button>
                                        ))}
                                    </div>
                                )}
                            </section>
                        ))}
                    </div>
                )}
            </div>,
            document.body,
        )
        : null;

    return (
        <>
            <div className="av-category-picker" ref={root}>
                <button
                    type="button"
                    className="av-category-picker-trigger"
                    disabled={disabled}
                    aria-label={label}
                    aria-haspopup="dialog"
                    aria-expanded={open}
                    aria-controls={panelId}
                    onClick={() => setOpen(current => !current)}
                >
                    <span
                        className="av-category-picker-icon"
                        aria-hidden="true"
                    >
                        {selected?.icon ?? '🗂️'}
                    </span>
                    <span className="av-category-picker-copy">
                        <strong>
                            {selectedParentName
                                ? `${selectedParentName} › ${selectedDisplayName}`
                                : selectedDisplayName}
                        </strong>
                        {selected && (
                            <small>
                                {selectedParentName
                                    ?? t(
                                        'transactionManagement.categoryPickerHelp',
                                    )}
                            </small>
                        )}
                    </span>
                    <span
                        className="av-category-picker-chevron"
                        aria-hidden="true"
                    >
                        {open ? '▴' : '▾'}
                    </span>
                </button>
            </div>

            {pickerPanel}
        </>
    );
}
