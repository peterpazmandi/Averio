import { apiFetch } from '../../../shared/api/apiFetch';

export type TransactionKind = 'Income' | 'Expense';
export type TransactionVisibility = 'Household' | 'Private';
export type TransactionStatus = 'Posted' | 'Voided';

export interface TransactionDto {
    id: string;
    accountId: string;
    accountName: string;
    currency: string;
    kind: TransactionKind;
    visibility: TransactionVisibility;
    amount: string;
    description: string;
    note: string | null;
    occurredOn: string;
    status: TransactionStatus;
    createdAt: string;
    updatedAt: string;
    canModify: boolean;
}

export interface TransactionPageDto {
    items: TransactionDto[];
    hasMore: boolean;
}

export interface TransactionInputDto {
    accountId: string;
    kind: TransactionKind;
    visibility: TransactionVisibility;
    amount: string;
    description: string;
    note: string | null;
    occurredOn: string;
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
    period: 'Day' | 'Month';
    periods: CashFlowPeriodDto[];
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

function queryString(filters: TransactionFilters) {
    const query = new URLSearchParams();
    Object.entries(filters).forEach(([key, value]) => {
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

export const getCashFlow = (
    householdId: string,
    from: string,
    to: string,
    period: 'Day' | 'Month' = 'Month',
    signal?: AbortSignal,
) => {
    const query = new URLSearchParams({ from, to, period });
    return apiFetch<CashFlowDto>(
        `/api/v1/households/${encodeURIComponent(householdId)}/statistics/cash-flow?${query}`,
        { signal },
    );
};
