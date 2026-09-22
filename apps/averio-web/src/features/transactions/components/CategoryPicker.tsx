import {
    type CSSProperties,
    useEffect,
    useId,
    useMemo,
    useRef,
    useState,
} from 'react';
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
    const searchInput = useRef<HTMLInputElement>(null);
    const panelId = useId();
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');

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
        if (!open) return;

        function onPointerDown(event: PointerEvent) {
            if (!root.current?.contains(event.target as Node)) {
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

        document.addEventListener('pointerdown', onPointerDown);
        document.addEventListener('keydown', onKeyDown);
        const frame = requestAnimationFrame(
            () => searchInput.current?.focus(),
        );

        return () => {
            cancelAnimationFrame(frame);
            document.removeEventListener('pointerdown', onPointerDown);
            document.removeEventListener('keydown', onKeyDown);
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

    return (
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
                <span className="av-category-picker-icon" aria-hidden="true">
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
                                ?? t('transactionManagement.categoryPickerHelp')}
                        </small>
                    )}
                </span>
                <span className="av-category-picker-chevron" aria-hidden="true">
                    {open ? '▴' : '▾'}
                </span>
            </button>

            {open && (
                <div
                    id={panelId}
                    className="av-category-picker-panel"
                    role="dialog"
                    aria-label={label}
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
                                    >
                                        <span
                                            className="av-category-parent-icon"
                                            aria-hidden="true"
                                        >
                                            {parent.icon}
                                        </span>
                                        <span>
                                            <strong>{parentName}</strong>
                                            <small>{children.length}</small>
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
                </div>
            )}
        </div>
    );
}
