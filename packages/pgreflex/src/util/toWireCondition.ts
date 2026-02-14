import type { AnyPgTable } from "drizzle-orm/pg-core";
import type { Condition } from "../drizzle";
import { Operand, type ConditionSet } from "../generated/protocol";

type WriteCondition = ConditionSet["conditions"][number];
type WireConditionValue = Omit<WriteCondition, "column" | "operand">;

export function toWriteCondition<T extends AnyPgTable>([
  col,
  op,
  val,
]: Condition<T>): ConditionSet["conditions"][number] {
  return {
    column: col as string,
    operand: operands[op],
    ...value(val),
  };
}

const operands: Record<Condition<any>[1], Operand> = {
  "==": Operand.EQ,
  "!=": Operand.NEQ,
  "<": Operand.LT,
  "<=": Operand.LTE,
  ">": Operand.GT,
  ">=": Operand.GTE,
  like: Operand.LIKE,
};

function value(v: string | Date | number | boolean | null): WireConditionValue {
  if (v === null || v === undefined) {
    return { isNull: true };
  }

  if (typeof v === "string") {
    return { str: v };
  }

  if (typeof v === "number") {
    return { num: v };
  }

  if (typeof v === "boolean") {
    return { b: v };
  }

  if (v instanceof Date) {
    return { timestampMicros: v.getTime() };
  }

  assertNever(v);
  throw new Error("Unreachable!");
}

function assertNever(x: never) {}
