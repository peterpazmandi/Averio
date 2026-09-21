import { queryOptions } from '@tanstack/react-query';
import { getCashFlow, getTransaction, getTransactions } from './transactionApi';
import type { TransactionFilters } from './transactionApi';

export const transactionQueryKeys = {
    all: (userId: string, householdId: string) =>
        ['transactions', userId, householdId] as const,
    list: (userId: string, householdId: string, filters: TransactionFilters) =>
        [...transactionQueryKeys.all(userId, householdId), 'list', filters] as const,
    detail: (userId: string, householdId: string, transactionId: string) =>
        [...transactionQueryKeys.all(userId, householdId), 'detail', transactionId] as const,
    cashFlow: (userId: string, householdId: string, from: string, to: string, period: 'Day' | 'Month') =>
        ['statistics', userId, householdId, 'cash-flow', from, to, period] as const,
};

export const transactionQueries = {
    list: (
        userId: string,
        householdId: string,
        filters: TransactionFilters,
    ) => queryOptions({
        queryKey: transactionQueryKeys.list(userId, householdId, filters),
        queryFn: ({ signal }) => getTransactions(householdId, filters, signal),
    }),
    detail: (
        userId: string,
        householdId: string,
        transactionId: string,
    ) => queryOptions({
        queryKey: transactionQueryKeys.detail(userId, householdId, transactionId),
        queryFn: ({ signal }) => getTransaction(householdId, transactionId, signal),
    }),
    cashFlow: (
        userId: string,
        householdId: string,
        from: string,
        to: string,
        period: 'Day' | 'Month' = 'Month',
    ) => queryOptions({
        queryKey: transactionQueryKeys.cashFlow(userId, householdId, from, to, period),
        queryFn: ({ signal }) => getCashFlow(householdId, from, to, period, signal),
    }),
};
