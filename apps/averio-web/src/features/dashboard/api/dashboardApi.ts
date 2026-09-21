import { apiFetch } from '../../../shared/api/apiFetch';
import type {
    AccountResponse,
    DashboardResponse,
} from '../../../shared/api/generated';

export interface DashboardCashFlowPeriod {
    currency: string;
    periodStart: string;
    incomeAmount: string;
    expenseAmount: string;
    transactionCount: number;
}

export interface DashboardLatestTransaction {
    id: string;
    accountId: string;
    accountName: string;
    currency: string;
    kind: 'Income' | 'Expense';
    visibility: 'Household' | 'Private';
    amount: string;
    description: string;
    note: string | null;
    occurredOn: string;
    status: 'Posted' | 'Voided';
    createdAt: string;
    updatedAt: string;
    canModify: boolean;
}

export type DashboardAccount = AccountResponse & {
    currentBalanceAmount: string;
};

export type DashboardData = Omit<DashboardResponse, 'accounts'> & {
    accounts: DashboardAccount[];
    cashFlow: DashboardCashFlowPeriod[];
    latestTransactions: DashboardLatestTransaction[];
};

export function getDashboard(
    householdId: string,
    signal?: AbortSignal,
): Promise<DashboardData> {
    const id = encodeURIComponent(householdId);

    return apiFetch<DashboardData>(
        `/api/v1/households/${id}/dashboard`,
        { signal },
    );
}
