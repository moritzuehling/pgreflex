

class Condition
{
  public required string Column { get; set; }

  public required Operation Operation { get; set; }

  public SerializedValue Value { get; set; }

  public struct SerializedValue
  {
    string String;
    double Number;
    DateTime DateTime;
  }
}
