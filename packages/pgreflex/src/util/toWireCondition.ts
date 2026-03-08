import type { AnyPgTable } from "drizzle-orm/pg-core";
import type { Condition } from "../drizzle";
import {
  Operand,
  type ColValue,
  type ConditionSet,
} from "../generated/protocol";

type WireConditionValue = ColValue;

export function toWriteCondition<T extends AnyPgTable>([
  col,
  op,
  val,
]: Condition<T>): ConditionSet["conditions"][number] {
  return {
    column: col as string,
    operand: operands[op],
    value: value(val),
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
  in: Operand.IN,
};

function value(
  v: string | Date | number | boolean | null | string[] | number[],
): WireConditionValue[] {
  if (v === null || v === undefined) {
    return [{ isNull: true }];
  }

  if (typeof v === "string") {
    return [{ str: v }];
  }

  if (typeof v === "number") {
    return [{ num: v }];
  }

  if (typeof v === "boolean") {
    return [{ b: v }];
  }

  if (v instanceof Date) {
    return [{ timestampMicros: v.getTime() }];
  }

  if (v instanceof Object) {
    if (isStringArray(v)) {
      return v.map((a) => ({ str: a }));
    } else {
      return v.map((a) => ({ num: a }));
    }
  }

  assertNever(v);
  throw new Error("Unreachable!");
}

function isStringArray(x: string[] | number[]): x is string[] {
  return typeof x[0] === "string";
}

function assertNever(x: never) {}
