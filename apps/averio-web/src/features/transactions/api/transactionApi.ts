import {
    CategoriesApi,
    LabelsApi,
    StatisticsApi,
    TransactionsApi,
} from '../../../shared/api/generated';
import type {
    CashFlowPeriodResponse,
    CashFlowResponse,
    CategoryCashFlowPeriodResponse,
    CategoryCashFlowResponse,
    CategoryResponse,
    CreateLabelRequest,
    CreateTransactionRequest,
    LabelResponse,
    TransactionItemRequest,
    TransactionPageResponse,
    TransactionResponse,
    UpdateTransactionRequest,
} from '../../../shared/api/generated';
import { generatedApiConfiguration } from '../../../shared/api/generatedApiConfiguration';

export type TransactionKind = 'Income' | 'Expense';
export type TransactionVisibility = 'Household' | 'Private';
export type LabelScope = 'Household' | 'Account';
export type AggregatePeriod = 'Day' | 'Month' | 'Year';

export type CategoryDto = CategoryResponse;
export type LabelDto = LabelResponse;
export type TransactionDto = Omit<
    TransactionResponse,
    'kind' | 'visibility'
> & {
    kind: TransactionKind;
    visibility: TransactionVisibility;
};
export type TransactionPageDto = Omit<TransactionPageResponse, 'items'> & {
    items: TransactionDto[];
};
export type TransactionItemInputDto = TransactionItemRequest;
export type TransactionInputDto = Omit<
    CreateTransactionRequest,
    'kind' | 'visibility' | 'labelIds'
> & {
    kind: TransactionKind;
    visibility: TransactionVisibility;
    labelIds: string[];
};
export type UpdateTransactionInputDto = Omit<
    UpdateTransactionRequest,
    'kind' | 'visibility' | 'labelIds'
> & {
    kind: TransactionKind;
    visibility: TransactionVisibility;
    labelIds: string[];
};
export type CashFlowPeriodDto = CashFlowPeriodResponse;
export type CashFlowDto = CashFlowResponse;
export type CategoryCashFlowPeriodDto = CategoryCashFlowPeriodResponse;
export type CategoryCashFlowDto = CategoryCashFlowResponse;

export interface TransactionFilters {
    labelIds?: string[];
    activityId?: string;
    from?: string;
    to?: string;
    accountId?: string;
    kind?: TransactionKind;
    search?: string;
    includeArchived?: boolean;
    importedOnly?: boolean;
    pageSize?: number;
    cursorDate?: string;
    cursorCreatedAt?: string;
    cursorId?: string;
}

const transactions = new TransactionsApi(generatedApiConfiguration);
const categories = new CategoriesApi(generatedApiConfiguration);
const labels = new LabelsApi(generatedApiConfiguration);
const statistics = new StatisticsApi(generatedApiConfiguration);

export const getTransactions = (
    householdId: string,
    filters: TransactionFilters,
    signal?: AbortSignal,
): Promise<TransactionPageDto> =>
    transactions.apiV1HouseholdsHouseholdIdTransactionsGet(
        { householdId, ...filters, labelIds: filters.labelIds?.length ? filters.labelIds : undefined },
        { signal },
    ) as Promise<TransactionPageDto>;

export const getTransaction = (
    householdId: string,
    transactionId: string,
    signal?: AbortSignal,
): Promise<TransactionDto> =>
    transactions.apiV1HouseholdsHouseholdIdTransactionsTransactionIdGet(
        { householdId, transactionId },
        { signal },
    ) as Promise<TransactionDto>;

export const createTransaction = (
    householdId: string,
    request: TransactionInputDto,
) => transactions.apiV1HouseholdsHouseholdIdTransactionsPost({
    householdId,
    createTransactionRequest: request,
});

export const updateTransaction = (
    householdId: string,
    transactionId: string,
    request: UpdateTransactionInputDto,
) => transactions.apiV1HouseholdsHouseholdIdTransactionsTransactionIdPut({
    householdId,
    transactionId,
    updateTransactionRequest: request,
});

export const archiveTransaction = (
    householdId: string,
    transactionId: string,
    archived: boolean,
    expectedUpdatedAt: string,
) => transactions.apiV1HouseholdsHouseholdIdTransactionsTransactionIdArchivePut({
    householdId, transactionId,
    changeTransactionArchiveRequest: { archived, expectedUpdatedAt },
});

export const voidTransaction = (
    householdId: string,
    transactionId: string,
    expectedUpdatedAt: string,
) => transactions.apiV1HouseholdsHouseholdIdTransactionsTransactionIdDelete({
    householdId,
    transactionId,
    expectedUpdatedAt,
});

export const getCategories = (
    householdId: string,
    kind?: TransactionKind,
    signal?: AbortSignal,
) => categories.apiV1HouseholdsHouseholdIdCategoriesGet(
    { householdId, kind },
    { signal },
);

export const getLabels = (
    householdId: string,
    accountId?: string,
    signal?: AbortSignal,
) => labels.apiV1HouseholdsHouseholdIdLabelsGet(
    { householdId, accountId },
    { signal },
);

export const createLabel = (
    householdId: string,
    request: CreateLabelRequest,
) => labels.apiV1HouseholdsHouseholdIdLabelsPost({
    householdId,
    createLabelRequest: request,
});

export const getCashFlow = (
    householdId: string,
    from: string,
    to: string,
    period: AggregatePeriod = 'Month',
    signal?: AbortSignal,
) => statistics.apiV1HouseholdsHouseholdIdStatisticsCashFlowGet(
    { householdId, from, to, period },
    { signal },
);

export const getCategoryCashFlow = (
    householdId: string,
    from: string,
    to: string,
    period: AggregatePeriod = 'Month',
    signal?: AbortSignal,
) => statistics.apiV1HouseholdsHouseholdIdStatisticsCategoryCashFlowGet(
    { householdId, from, to, period },
    { signal },
);
