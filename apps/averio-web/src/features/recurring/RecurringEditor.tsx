import type { RecurringRuleResponse, SaveRecurringRuleRequest } from '../../shared/api/generated';
import type { RecurringType } from './RecurringTypeDialog';
import { TransactionEditor } from '../transactions/components/TransactionEditor';

export function RecurringEditor({ userId, householdId, rule, onSave, onClose, creationType = 'expense' }: {
    creationType?: RecurringType;
    userId: string; householdId: string; rule: RecurringRuleResponse | null;
    onSave: (value: SaveRecurringRuleRequest) => Promise<void>; onClose: () => void;
}) {
    return <TransactionEditor userId={userId} householdId={householdId} transaction={null}
        recurringRule={rule ?? undefined} initialKind={creationType === 'income' ? 'Income' : 'Expense'} initialReimbursement={creationType === 'reimbursement'} initialRecurring onSaveRecurring={onSave} onCancel={onClose} busy={false} />;
}
