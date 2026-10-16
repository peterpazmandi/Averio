import type { RecurringRuleResponse, SaveRecurringRuleRequest } from '../../shared/api/generated';
import { TransactionEditor } from '../transactions/components/TransactionEditor';

export function RecurringEditor({ userId, householdId, rule, onSave, onClose }: {
    userId: string; householdId: string; rule: RecurringRuleResponse | null;
    onSave: (value: SaveRecurringRuleRequest) => Promise<void>; onClose: () => void;
}) {
    return <TransactionEditor userId={userId} householdId={householdId} transaction={null}
        recurringRule={rule ?? undefined} initialRecurring onSaveRecurring={onSave} onCancel={onClose} busy={false} />;
}
