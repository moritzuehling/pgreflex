
using System.Runtime.Serialization;

enum Operation
{
  [EnumMember(Value = "==")]
  Equal,
  [EnumMember(Value = "==")]
  NotEquals,
  [EnumMember(Value = "==")]
  LowerThan,
  [EnumMember(Value = "==")]
  LowerThanEquals,
  [EnumMember(Value = "==")]
  GreatThan,
  [EnumMember(Value = "==")]
  GreatThanEquals,
  [EnumMember(Value = "==")]
  IsNull,
  [EnumMember(Value = "==")]
  IsNotNull,
  [EnumMember(Value = "==")]
  Like,
  [EnumMember(Value = "==")]
  ILike,
}
