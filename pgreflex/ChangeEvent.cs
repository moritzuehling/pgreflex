using Npgsql.PostgresTypes;

record ChangeEvent
{
  public required string Table { get; set; }
  public required string Schema { get; set; }

  public required List<ChangedColumn> ChangedColumns;
}

record ChangedColumn
{
  public required string ColumnName { get; set; }
  public required PostgresType ColumnType { get; set; }
  public object? Value { get; set; }
  public required Type ValType { get; set; }
}
