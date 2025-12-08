import {
  and,
  eq,
  gte,
  isNotNull,
  isNull,
  like,
  lt,
  gt,
  lte,
  type ColumnDataType,
  type SQL,
  asc,
  desc,
} from "drizzle-orm";
import type {
  AnyPgTable,
  PgColumn,
  PgDatabase,
  PgQueryResultHKT,
  SelectedFields,
} from "drizzle-orm/pg-core";

type AnyPgDb = PgDatabase<PgQueryResultHKT>;

type TypedColumns<T extends AnyPgTable, Q extends ColumnDataType> = {
  [C in keyof T["_"]["columns"]]: T["_"]["columns"][C]["dataType"] extends Q
    ? C
    : never;
}[keyof T["_"]["columns"]] &
  keyof T;
type NullableColumns<T extends AnyPgTable> = {
  [C in keyof T["$inferSelect"]]: null extends T["$inferSelect"][C] ? C : never;
}[keyof T["$inferSelect"]] &
  keyof T;

type Condition<T extends AnyPgTable> =
  | [
      TypedColumns<T, "string">,
      "==" | "!=" | "<" | ">" | "<=" | ">=" | "like",
      string
    ]
  | [TypedColumns<T, "number">, "==" | "!=" | "<" | ">" | "<=" | ">=", number]
  | [NullableColumns<T>, "==" | "!=", null];

interface SelectConfig<T extends AnyPgTable> {
  limit?: number;
  offset?: number;

  orderBy?: [TypedColumns<T, "string" | "number">, "desc" | "asc"][];
}

export async function selectSingle<DB extends AnyPgDb, T extends AnyPgTable>(
  db: DB,
  tbl: T,
  conditions: Condition<T>[]
) {
  const res = await select<DB, T>(db, tbl, conditions, {
    limit: 2,
  });
  if (res.length == 2) {
    throw new Error("There was more than one entry returned by the query");
  }
  if (res.length == 0) {
    throw new Error("Query returned no results");
  }

  return res[0];
}

export async function selectSingleOptional<
  DB extends AnyPgDb,
  T extends AnyPgTable
>(db: DB, tbl: T, conditions: Condition<T>[]) {
  const res = await select<DB, T>(db, tbl, conditions, {
    limit: 2,
  });
  if (res.length == 2) {
    throw new Error("There was more than one entry returned by the query");
  }
  return res.at(0);
}

export function select<DB extends AnyPgDb, T extends AnyPgTable>(
  db: DB,
  tbl: T,
  conditions: Condition<T>[],
  config: SelectConfig<T> = {}
) {
  const from = db.select().from<T>(tbl as any);
  const where = from.where(and(...conditions.map((c) => getCondition(tbl, c))));

  const limit = config.limit ? where.limit(config.limit) : where;
  const offset = config.offset ? limit.offset(config.offset) : limit;

  const orderBy = config.orderBy
    ? offset.orderBy(
        ...config.orderBy.map(([colName, order]) =>
          orderFns[order](tbl[colName] as PgColumn)
        )
      )
    : offset;

  return orderBy;
}

export function selectColumns<
  SF extends SelectedFields,
  DB extends AnyPgDb,
  T extends AnyPgTable
>(
  db: DB,
  tbl: T,
  select: SF,
  conditions: Condition<T>[],
  config: SelectConfig<T> = {}
) {
  const from = db.select(select).from<T>(tbl as any);

  const where = from.where(and(...conditions.map((c) => getCondition(tbl, c))));

  const limit = config.limit
    ? (where.limit(config.limit) as typeof where)
    : where;
  const offset = config.offset
    ? (limit.offset(config.offset) as typeof where)
    : limit;

  const orderBy = config.orderBy
    ? (offset.orderBy(
        ...config.orderBy.map(([colName, order]) =>
          orderFns[order](tbl[colName] as PgColumn)
        )
      ) as typeof where)
    : offset;

  return orderBy as typeof where;
}

function getCondition<T extends AnyPgTable>(
  t: T,
  [colName, op, v]: Condition<T>
): SQL {
  const c = t[colName] as PgColumn;

  switch (op) {
    case "like":
      return like(c, v);
    case "==":
      return v == null ? isNull(c) : eq(c, v);
    case "!=":
      return v == null ? isNotNull(c) : eq(c, v);
    default:
      return fns[op](c, v);
  }
}
const fns = {
  "<": lt,
  "<=": lte,
  ">": gt,
  ">=": gte,
} as const;

const orderFns = {
  asc,
  desc,
} as const;
