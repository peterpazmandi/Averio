import { apiFetch } from '../../../shared/api/apiFetch';

export type TransactionKind = 'Income' | 'Expense';
export type TransactionVisibility = 'Household' | 'Private';
export type TransactionStatus = 'Posted' | 'Voided';
export type LabelScope = 'Household' | 'Account';
export type CategoryKind = 'Income' | 'Expense' | 'Both';
export type AggregatePeriod = 'Day' | 'Month' | 'Year';

export interface CategoryDto {
    id: string;
    parentId: string | null;
    code: string;
    name: string;
    kind: CategoryKind;
    icon: string;
    accentColor: string;
    sortOrder: number;
    isBase: boolean;
}

export interface LabelDto {
    id: string;
    name: string;
    scope: LabelScope;
    accountId: string | null;
}

export interface TransactionItemDto {
    id: string;
    position: number;
    description: string;
    categoryId: string;
    categoryName: string;
    categoryIcon: string;
    amount: string;
}

export interface TransactionLabelDto {
    id: string;
    name: string;
    scope: LabelScope;
}

export interface TransactionDto {
    id: string;
    accountId: string;
    accountName: string;
    currency: string;
    kind: TransactionKind;
    visibility: TransactionVisibility;
    amount: string;
    description: string;
    merchant: string | null;
    note: string | null;
    occurredOn: string;
    status: TransactionStatus;
    createdAt: string;
    updatedAt: string;
    canModify: boolean;
    items: TransactionItemDto[];
    labels: TransactionLabelDto[];
}

export interface TransactionPageDto {
    items: TransactionDto[];
    hasMore: boolean;
}

export interface TransactionItemInputDto {
    description: string;
    categoryId: string;
    amount: string;
}

export interface TransactionInputDto {
    accountId: string;
    kind: TransactionKind;
    visibility: TransactionVisibility;
    merchant: string | null;
    note: string | null;
    occurredOn: string;
    items: TransactionItemInputDto[];
    labelIds: string[];
}

export interface UpdateTransactionInputDto extends TransactionInputDto {
    expectedUpdatedAt: string;
}

export interface CashFlowPeriodDto {
    currency: string;
    periodStart: string;
    incomeAmount: string;
    expenseAmount: string;
    transactionCount: number;
}

export interface CashFlowDto {
    from: string;
    to: string;
    period: AggregatePeriod;
    periods: CashFlowPeriodDto[];
}

export interface CategoryCashFlowPeriodDto {
    categoryId: string;
    categoryName: string;
    categoryIcon: string;
    currency: string;
    periodStart: string;
    incomeAmount: string;
    expenseAmount: string;
    itemCount: number;
}

export interface CategoryCashFlowDto {
    from: string;
    to: string;
    period: AggregatePeriod;
    periods: CategoryCashFlowPeriodDto[];
}

export interface TransactionFilters {
    from?: string;
    to?: string;
    accountId?: string;
    kind?: TransactionKind;
    search?: string;
    pageSize?: number;
    cursorDate?: string;
    cursorCreatedAt?: string;
}

function queryString<T extends object>(filters: T) {
    const query = new URLSearchParams();
    Object.entries(filters as Record<string, unknown>).forEach(([key, value]) => {
        if (value !== undefined && value !== '') query.set(key, String(value));
    });
    const value = query.toString();
    return value ? `?${value}` : '';
}

export const getTransactions = (
    householdId: string,
    filters: TransactionFilters,
    signal?: AbortSignal,
) => apiFetch<TransactionPageDto>(
    `/api/v1/households/${encodeURIComponent(householdId)}/transactions${queryString(filters)}`,
    { signal },
);

export const getTransaction = (
    householdId: string,
    transactionId: string,
    signal?: AbortSignal,
) => apiFetch<TransactionDto>(
    `/api/v1/households/${encodeURIComponent(householdId)}/transactions/${encodeURIComponent(transactionId)}`,
    { signal },
);

export const createTransaction = (
    householdId: string,
    request: TransactionInputDto,
) => apiFetch<{ id: string }>(
    `/api/v1/households/${encodeURIComponent(householdId)}/transactions`,
    { method: 'POST', body: JSON.stringify(request) },
);

export const updateTransaction = (
    householdId: string,
    transactionId: string,
    request: UpdateTransactionInputDto,
) => apiFetch<void>(
    `/api/v1/households/${encodeURIComponent(householdId)}/transactions/${encodeURIComponent(transactionId)}`,
    { method: 'PUT', body: JSON.stringify(request) },
);

export const voidTransaction = (
    householdId: string,
    transactionId: string,
    expectedUpdatedAt: string,
) => apiFetch<void>(
    `/api/v1/households/${encodeURIComponent(householdId)}/transactions/${encodeURIComponent(transactionId)}?expectedUpdatedAt=${encodeURIComponent(expectedUpdatedAt)}`,
    { method: 'DELETE' },
);

export const getCategories = (
    householdId: string,
    kind?: TransactionKind,
    signal?: AbortSignal,
) => apiFetch<CategoryDto[]>(
    `/api/v1/households/${encodeURIComponent(householdId)}/categories${queryString({ kind })}`,
    { signal },
);

export const getLabels = (
    householdId: string,
    accountId?: string,
    signal?: AbortSignal,
) => apiFetch<LabelDto[]>(
    `/api/v1/households/${encodeURIComponent(householdId)}/labels${queryString({ accountId })}`,
    { signal },
);

export const createLabel = (
    householdId: string,
    request: { name: string; scope: LabelScope; accountId: string | null },
) => apiFetch<{ id: string }>(
    `/api/v1/households/${encodeURIComponent(householdId)}/labels`,
    { method: 'POST', body: JSON.stringify(request) },
);

export const getCashFlow = (
    householdId: string,
    from: string,
    to: string,
    period: AggregatePeriod = 'Month',
    signal?: AbortSignal,
) => {
    const query = new URLSearchParams({ from, to, period });
    return apiFetch<CashFlowDto>(
        `/api/v1/households/${encodeURIComponent(householdId)}/statistics/cash-flow?${query}`,
        { signal },
    );
};

export const getCategoryCashFlow = (
    householdId: string,
    from: string,
    to: string,
    period: AggregatePeriod = 'Month',
    signal?: AbortSignal,
) => {
    const query = new URLSearchParams({ from, to, period });
    return apiFetch<CategoryCashFlowDto>(
        `/api/v1/households/${encodeURIComponent(householdId)}/statistics/category-cash-flow?${query}`,
        { signal },
    );
};
