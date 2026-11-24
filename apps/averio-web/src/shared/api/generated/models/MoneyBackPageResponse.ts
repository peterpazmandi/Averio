/* tslint:disable */
/* eslint-disable */
import type { MoneyBackItemResponse } from './MoneyBackItemResponse';
import { MoneyBackItemResponseFromJSON, MoneyBackItemResponseToJSON } from './MoneyBackItemResponse';
export interface MoneyBackPageResponse { items: Array<MoneyBackItemResponse>; hasMore: boolean; offset: number; limit: number; }
export function instanceOfMoneyBackPageResponse(value: object): value is MoneyBackPageResponse { return 'items' in value && 'hasMore' in value && 'offset' in value && 'limit' in value; }
export function MoneyBackPageResponseFromJSON(json: any): MoneyBackPageResponse { return MoneyBackPageResponseFromJSONTyped(json, false); }
export function MoneyBackPageResponseFromJSONTyped(json: any, _ignoreDiscriminator: boolean): MoneyBackPageResponse { if (json == null) return json; return { items: (json.items as Array<any>).map(MoneyBackItemResponseFromJSON), hasMore: json.hasMore, offset: json.offset, limit: json.limit }; }
export function MoneyBackPageResponseToJSON(value?: MoneyBackPageResponse | null): any { if (value == null) return value; return { items: value.items.map(MoneyBackItemResponseToJSON), hasMore: value.hasMore, offset: value.offset, limit: value.limit }; }
