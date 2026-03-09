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
  type Table,
  type Column,
  type SelectedFieldsFlat,
  arrayContains,
  sql,
} from "drizzle-orm";
import {
  getTableConfig,
  type PgDatabase,
  type PgQueryResultHKT,
  type PgSelectBuilder,
} from "drizzle-orm/pg-core";
import type { ReflexSubscribeTo } from "./connection";
import { toWireCondition } from "./util/toWireCondition";

export type AnyPgDb = PgDatabase<PgQueryResultHKT>;

type SelectedFields<T extends Table> = SelectedFieldsFlat<
  T["_"]["columns"][string]
>;

type TypedColumns<T extends Table, Q extends ColumnDataType> = {
  [C in keyof T["_"]["columns"]]: T["_"]["columns"][C]["dataType"] extends Q
    ? C
    : never;
}[keyof T["_"]["columns"]] &
  keyof T;
type NullableColumns<T extends Table> = {
  [C in keyof T["$inferSelect"]]: null extends T["$inferSelect"][C] ? C : never;
}[keyof T["$inferSelect"]] &
  keyof T;

export type Condition<T extends Table> =
  | [
      TypedColumns<T, "string">,
      "==" | "!=" | "<" | ">" | "<=" | ">=" | "like",
      string,
    ]
  | [TypedColumns<T, "string">, "in", string[]]
  | [TypedColumns<T, "number">, "==" | "!=" | "<" | ">" | "<=" | ">=", number]
  | [TypedColumns<T, "number">, "in", number[]]
  | [TypedColumns<T, "date">, "==" | "!=" | "<" | ">" | "<=" | ">=", Date]
  | [TypedColumns<T, "date">, "in", Date[]]
  | [NullableColumns<T>, "==" | "!=", null]
  | undefined
  | false
  | null;

interface SelectConfig<T extends Table> {
  limit?: number;
  offset?: number;

  orderBy?: [
    TypedColumns<
      T,
      | "string"
      | "number"
      | "date"
      | "duration"
      | "dateDuration"
      | "boolean"
      | "bigint"
      | "localDate"
      | "localTime"
    >,
    "desc" | "asc",
  ][];
}

export function reflexDb<DB extends AnyPgDb>(
  db: DB,
  subscribeTo?: ReflexSubscribeTo,
) {
  return {
    async selectSingle<T extends Table>(tbl: T, conditions: Condition<T>[]) {
      await subscribeTo?.({
        table: getTableConfig(tbl).name,
        schema: getTableConfig(tbl).schema as string,
        conditions: conditions
          .filter((a) => a !== null && a !== false && a !== undefined)
          .map(toWireCondition)
          .filter((a) => a != null),
      });

      return await selectSingle(db, tbl, conditions);
    },
    async selectSingleOptional<T extends Table>(
      tbl: T,
      conditions: Condition<T>[],
    ) {
      await subscribeTo?.({
        table: getTableConfig(tbl).name,
        schema: getTableConfig(tbl).schema as string,
        conditions: conditions
          .filter((a) => a !== null && a !== false && a !== undefined)
          .map(toWireCondition),
      });
      return await selectSingleOptional(db, tbl, conditions);
    },
    async select<SF extends SelectedFields<T>, T extends Table>(
      tbl: T,
      conditions: Condition<T>[],
      config: SelectConfig<T> = {},
    ) {
      await subscribeTo?.({
        table: getTableConfig(tbl).name,
        schema: getTableConfig(tbl).schema as string,
        conditions: conditions
          .filter((a) => a !== null && a !== false && a !== undefined)
          .map(toWireCondition),
      });
      return await select(db, tbl, conditions, config);
    },
    async selectColumns<
      SF extends SelectedFieldsFlat<T["_"]["columns"][string]>,
      T extends Table,
    >(
      tbl: T,
      select: SF,
      conditions: Condition<T>[],
      config: SelectConfig<T> = {},
    ) {
      await subscribeTo?.({
        table: getTableConfig(tbl).name,
        schema: getTableConfig(tbl).schema as string,
        conditions: conditions
          .filter((a) => a !== null && a !== false && a !== undefined)
          .map(toWireCondition),
      });
      return await selectColumns(db, tbl, select, conditions, config);
    },
  };
}

export type ReflexDB<DB extends AnyPgDb> = ReturnType<typeof reflexDb<DB>>;

async function selectSingle<DB extends AnyPgDb, T extends Table>(
  db: DB,
  tbl: T,
  conditions: Condition<T>[],
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

async function selectSingleOptional<DB extends AnyPgDb, T extends Table>(
  db: DB,
  tbl: T,
  conditions: Condition<T>[],
) {
  const res = await select<DB, T>(db, tbl, conditions, {
    limit: 2,
  });
  if (res.length == 2) {
    throw new Error("There was more than one entry returned by the query");
  }
  return res.at(0);
}

function select<DB extends AnyPgDb, T extends Table>(
  db: DB,
  tbl: T,
  conditions: Condition<T>[],
  config: SelectConfig<T> = {},
) {
  const from = db.select().from<T>(tbl as any);
  const where = from.where(and(...conditions.map((c) => getCondition(tbl, c))));

  const limit = config.limit ? where.limit(config.limit) : where;
  const offset = config.offset ? limit.offset(config.offset) : limit;

  const orderBy = config.orderBy
    ? offset.orderBy(
        ...config.orderBy.map(([colName, order]) =>
          orderFns[order](tbl[colName] as Column),
        ),
      )
    : offset;

  return orderBy;
}

function selectColumns<
  SF extends SelectedFields<T>,
  DB extends AnyPgDb,
  T extends Table,
>(
  db: DB,
  tbl: T,
  select: SF,
  conditions: Condition<T>[],
  config: SelectConfig<T> = {},
): Promise<Awaited<PgSelectBuilder<SF, "db">>> {
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
          orderFns[order](tbl[colName] as Column),
        ),
      ) as typeof where)
    : offset;

  return orderBy as unknown as Promise<Awaited<PgSelectBuilder<SF, "db">>>;
}

function getCondition<T extends Table>(t: T, condition: Condition<T>): SQL {
  if (!condition) {
    return sql`true`;
  }

  const [colName, op, v] = condition;

  const c = t[colName] as Column;

  switch (op) {
    case "like":
      return like(c, v);
    case "==":
      return v == null ? isNull(c) : eq(c, v);
    case "!=":
      return v == null ? isNotNull(c) : eq(c, v);
    case "in":
      return arrayContains(c, v);
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
