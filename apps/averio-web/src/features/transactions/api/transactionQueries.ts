import { queryOptions } from '@tanstack/react-query';
import {
    getCashFlow,
    getCategories,
    getCategoryCashFlow,
    getLabels,
    getTransaction,
    getTransactions,
} from './transactionApi';
import type {
    AggregatePeriod,
    TransactionFilters,
    TransactionKind,
} from './transactionApi';

export const transactionQueryKeys = {
    all: (userId: string, householdId: string) =>
        ['transactions', userId, householdId] as const,
    list: (userId: string, householdId: string, filters: TransactionFilters) =>
        [...transactionQueryKeys.all(userId, householdId), 'list', filters] as const,
    detail: (userId: string, householdId: string, transactionId: string) =>
        [...transactionQueryKeys.all(userId, householdId), 'detail', transactionId] as const,
    categories: (userId: string, householdId: string, kind?: TransactionKind) =>
        ['categories', userId, householdId, kind ?? 'all'] as const,
    labels: (userId: string, householdId: string, accountId?: string) =>
        ['labels', userId, householdId, accountId ?? 'household'] as const,
    cashFlow: (
        userId: string,
        householdId: string,
        from: string,
        to: string,
        period: AggregatePeriod,
    ) => ['statistics', userId, householdId, 'cash-flow', from, to, period] as const,
    categoryCashFlow: (
        userId: string,
        householdId: string,
        from: string,
        to: string,
        period: AggregatePeriod,
    ) => ['statistics', userId, householdId, 'category-cash-flow', from, to, period] as const,
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
    categories: (
        userId: string,
        householdId: string,
        kind?: TransactionKind,
    ) => queryOptions({
        queryKey: transactionQueryKeys.categories(userId, householdId, kind),
        queryFn: ({ signal }) => getCategories(householdId, kind, signal),
    }),
    labels: (
        userId: string,
        householdId: string,
        accountId?: string,
    ) => queryOptions({
        queryKey: transactionQueryKeys.labels(userId, householdId, accountId),
        queryFn: ({ signal }) => getLabels(householdId, accountId, signal),
        enabled: Boolean(accountId),
    }),
    cashFlow: (
        userId: string,
        householdId: string,
        from: string,
        to: string,
        period: AggregatePeriod = 'Month',
    ) => queryOptions({
        queryKey: transactionQueryKeys.cashFlow(userId, householdId, from, to, period),
        queryFn: ({ signal }) => getCashFlow(householdId, from, to, period, signal),
    }),
    categoryCashFlow: (
        userId: string,
        householdId: string,
        from: string,
        to: string,
        period: AggregatePeriod = 'Month',
    ) => queryOptions({
        queryKey: transactionQueryKeys.categoryCashFlow(
            userId,
            householdId,
            from,
            to,
            period,
        ),
        queryFn: ({ signal }) =>
            getCategoryCashFlow(householdId, from, to, period, signal),
    }),
};
