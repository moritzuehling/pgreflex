export type WireConditionSet = {
  table: {
    schema: string | undefined;
    name: string;
  };
  conditions: WireCondition[];
};

type WireCondition = [
  string,
  "==" | "!=" | "<" | ">" | "<=" | ">=" | "like",
  string | number | null
];

export type ClientToServerMessage = {
  messageType: "subscribe";
  requestId: number;
  groupId: string;
  conditionSet: WireConditionSet;
};

export type ServerToClientMessage =
  | {
      messageType: "subscribeSucess";
      requestId: number;
    }
  | {
      messageType: "invalidate";
      groupId: string;
    };
