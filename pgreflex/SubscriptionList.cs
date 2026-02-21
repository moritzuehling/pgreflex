global using TableSubList = System.Collections.Immutable.ImmutableList<TableSubscription>;
global using SubscriptionState = System.Collections.Immutable.ImmutableDictionary<string, System.Collections.Immutable.ImmutableList<TableSubscription>>;
using System.Collections.Immutable;
using Pgreflex.Protocol;
using System.Text.Json.Serialization;
using System.Text.Json;


record TableSubscription
{
  public required string GroupId { get; set; }
  public required Connection Connection { get; set; }

  public required ConditionSet Conditions { get; set; }
}



class SubscriptionList
{
  SubscriptionState _tables = SubscriptionState.Empty;
  public SubscriptionState Tables => _tables;

  public void Add(string tableName, TableSubscription subscription)
  {
    var origTables = _tables;
    origTables.TryGetValue(tableName, out TableSubList? tableState);
    tableState = tableState ?? TableSubList.Empty;

    var newTables = origTables.SetItem(tableName, tableState.Add(subscription));

    if (_tables != origTables)
      Error()("wtf violation");

    _tables = newTables;
    Debug();
  }

  public void Remove(List<string> groupIDs)
  {

    var origTables = _tables;
    var newTables = origTables;
    foreach (var t in origTables)
      newTables = origTables.SetItem(t.Key, t.Value.RemoveAll(a => groupIDs.Contains(a.GroupId)));


    _tables = newTables;
    Debug();
  }

  private void Debug()
  {


    var str = JsonSerializer.Serialize(_tables, typeof(ImmutableDictionary<string, TableSubList>), new JsonSerializerOptions()
    {
      IndentSize = 2,
      WriteIndented = true,
    });
    File.WriteAllText("/tmp/subs.json", str);
  }

}
