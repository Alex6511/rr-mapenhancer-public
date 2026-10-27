using System.Text.Json;
using MapEnhancer;
using Track;
int checks=0;
void Check(bool ok,string reason){if(!ok)throw new Exception(reason);checks++;}
Turntable Make(string id){var t=new Turntable{id=id};for(int i=0;i<8;i++)t.nodes.Add(new TrackNode());Graph.Shared.Tables[id]=t;return t;}
foreach(string id in new[]{"whittier","modded-map-table","bryson-addition"}){
 Graph.Shared=new Graph();
 var table=Make(id);
 table.Angle=0;
 var h=new TurntableHelper();h.Bind(table);
 Check(h.IsInitialized,"Graph table must be marker-eligible without a scene controller.");
 Check(h.Controller==null,"Do not invent a controller.");
 Check(!h.Rotate180(),"No controller must not be reported as successful rotation.");
 var c=new TurntableController{turntable=table};Graph.Shared.Controllers[id]=c;
 Check(ReferenceEquals(h.Controller,c),"Resolve a controller that registers after marker creation.");
 Graph.Shared.Segments[table.nodes[0]]=new(){new TrackSegment()};
 Check(h.Rotate180()&&table.Angle==180&&table.StopIndex==4,"Snap to the opposite connected stop.");
 Graph.Shared.Segments.Clear();table.Angle=0;table.StopIndex=0;
 Graph.Shared.Segments[table.nodes[1]]=new(){new TrackSegment()};
 Check(h.ActiveTrackIndexes.SequenceEqual(new[]{1,5}),"Both bridge orientations must recognize the connected pit track.");
 Check(h.GetNextTrackIndex()==1,"Clockwise chooses the nearest connected stop.");
 Check(h.GetNextTrackIndex(false)==5,"Counterclockwise chooses the nearest connected stop.");
 Check(h.RotateClockwise()&&table.Angle==45&&table.StopIndex==1,"Next snaps immediately to the connected stop.");
 Check(h.RotateCounterClockwise()&&table.Angle==225&&table.StopIndex==5,"Previous snaps immediately, wrapping to the connected stop.");
 Check(!h.MoveToIndex(2),"An unconnected target must be rejected.");
 c.isActiveAndEnabled=false;
 Check(h.Controller==null&&!h.Rotate180(),"Disabled native controllers cannot receive rotation orders.");
 var replacement=new TurntableController{turntable=table};Graph.Shared.Controllers[id]=replacement;
 Check(ReferenceEquals(h.Controller,replacement),"Rebind after controller replacement.");
 Check(h.Rotate180()&&table.Angle==45&&table.StopIndex==1,"A replacement receives the snap command.");
 Graph.Shared.Tables.Remove(id);
 Check(!h.IsInitialized&&!h.Rotate180(),"Removing the graph table invalidates its marker and commands.");
 var newer=Make(id);
 Check(!h.IsInitialized,"Reusing an ID must not revive stale table references.");
 h.Bind(newer);
 Check(h.IsInitialized,"Bind the replacement graph table.");
 Graph.Shared=new Graph();
 Check(!h.IsInitialized,"World replacement must invalidate old helpers.");
}
Graph.Shared=new Graph();
var hiddenTable=Make("runtime-pack-table");
var hiddenHelper=new TurntableHelper();hiddenHelper.Bind(hiddenTable);
var prefab=new TurntableController{turntable=hiddenTable};prefab.gameObject.scene.Valid=false;
UnityEngine.Resources.Existing=new UnityEngine.Object[]{prefab};
Check(hiddenHelper.Controller==null,"A loaded prefab asset must never receive map commands.");
var hiddenController=new TurntableController{turntable=hiddenTable};
UnityEngine.Resources.Existing=new UnityEngine.Object[]{prefab,hiddenController};
Check(ReferenceEquals(hiddenHelper.Controller,hiddenController),"Discover a valid-scene runtime controller missing from the normal registry.");
Graph.Shared.Segments[hiddenTable.nodes[0]]=new(){new TrackSegment()};
Check(hiddenHelper.Rotate180()&&hiddenTable.Angle==180,"Runtime pack controller receives native snap commands.");
Check(prefab.TargetAngle==null,"Prefab asset remains untouched.");
hiddenController.Destroyed=true;
Check(hiddenHelper.Controller==null,"A destroyed runtime controller cannot be reused.");
UnityEngine.Resources.Existing=Array.Empty<UnityEngine.Object>();
Graph.Shared=new Graph();
var snapTable=Make("snap-contract");
Graph.Shared.Segments[snapTable.nodes[0]]=new(){new TrackSegment()};
Graph.Shared.Segments[snapTable.nodes[1]]=new(){new TrackSegment()};
var snapController=new TurntableController{turntable=snapTable,TargetAngle=270,TargetSpeedPercent=1,Speed=7,WasMoving=true};
Graph.Shared.Controllers[snapTable.id]=snapController;
var snapHelper=new TurntableHelper();snapHelper.Bind(snapTable);
int completed=0;snapHelper.OnRotationComplete=()=>completed++;
Game.State.StateManager.Messages.Clear();
Check(snapHelper.RotateClockwise()&&snapTable.StopIndex==1&&snapTable.Angle==45,"One click completes exactly one next stop.");
Check(snapController.TargetAngle==null&&snapController.TargetSpeedPercent==0&&snapController.Speed==0&&!snapController.WasMoving,"Snap cancels motor orders and residual speed.");
Check(snapController.Calls.SequenceEqual(new[]{"disconnect","angle","connect"}),"Disconnect old track before moving and reconnect at destination.");
Check(completed==1,"Refresh map once when snap completes.");
Check(Game.State.StateManager.Messages.Count==1&&Game.State.StateManager.Messages[0].StopIndex==1&&Game.State.StateManager.Messages[0].Angle==45,"Publish the completed stop even without a moving-transmitter cycle.");
Check(snapHelper.RotateClockwise()&&snapTable.StopIndex==4,"Repeated next advances from the newly snapped stop.");
Check(snapHelper.RotateCounterClockwise()&&snapTable.StopIndex==1,"Previous returns to the preceding connected stop.");
Game.State.StateManager.IsHost=false;
Check(!snapHelper.RotateClockwise()&&snapTable.StopIndex==1,"Clients must not mutate authoritative turntable state.");
Game.State.StateManager.IsHost=true;
snapTable.Blocked=true;
int calls=snapController.Calls.Count, messages=Game.State.StateManager.Messages.Count;
Check(!snapHelper.RotateClockwise()&&snapController.Calls.Count==calls&&Game.State.StateManager.Messages.Count==messages,"Fouled bridge rejects before changing native state.");
snapTable.Blocked=false;
Model.TrainController.Shared.Cars["bridge-A"]=new Model.Car();
Model.TrainController.Shared.Cars["pit-4"]=new Model.Car();
Check(!snapHelper.RotateClockwise()&&snapTable.StopIndex==1,"Occupied destination rejects a conflicting bridge car.");
Model.TrainController.Shared.Cars.Clear();
Check(snapHelper.RotateClockwise()&&snapTable.StopIndex==4,"Cleared destination accepts the next command.");
snapTable.StopIndex=null;
Check(!snapHelper.Rotate180(),"Half-turn requires an aligned stop.");
if(args.Length > 0)
{
 using var doc=JsonDocument.Parse(File.ReadAllText(args[0]));
 var graphData=doc.RootElement.GetProperty("graph");
 var segments=graphData.GetProperty("segments").EnumerateObject().Select(p=>(
     A:p.Value.GetProperty("a").GetString(),B:p.Value.GetProperty("b").GetString())).ToArray();
 foreach(var data in graphData.GetProperty("turntables").EnumerateObject())
 {
  Graph.Shared=new Graph();
  var table=new Turntable{id=data.Name};
  var ids=data.Value.GetProperty("nodeIds").EnumerateArray().Select(v=>v.GetString()!).ToArray();
  foreach(var id in ids)
  {
   var node=new TrackNode();table.nodes.Add(node);
   Graph.Shared.Segments[node]=segments.Where(e=>e.A==id||e.B==id).Select(_=>new TrackSegment()).ToList();
  }
  Check(ids.Length==data.Value.GetProperty("subdivisions").GetInt32(),"Native table stop count must match its graph nodes.");
  Graph.Shared.Tables[table.id]=table;
  int current=data.Value.GetProperty("defaultStopIndex").GetInt32();
  table.Angle=table.AngleForIndex(current);
  var helper=new TurntableHelper();helper.Bind(table);
  Check(helper.IsInitialized&&helper.Controller==null,"Native map table is discoverable without scenery.");
  var connected=Enumerable.Range(0,ids.Length).Where(i=>segments.Any(e=>
      e.A==ids[i]||e.B==ids[i]||e.A==ids[(i+ids.Length/2)%ids.Length]||e.B==ids[(i+ids.Length/2)%ids.Length])).ToHashSet();
  Check(connected.SetEquals(helper.ActiveTrackIndexes),"Connected stops must match the installed base-map topology.");
  var controller=new TurntableController{turntable=table};Graph.Shared.Controllers[table.id]=controller;
  int Next(int direction)
  {
   for(int n=1;n<ids.Length;n++){int i=(current+direction*n+ids.Length)%ids.Length;if(connected.Contains(i))return i;}
   return -1;
  }
  var cw=Next(1);var ccw=Next(-1);
  Check(helper.GetNextTrackIndex()==cw,"Native topology clockwise target.");
  Check(helper.GetNextTrackIndex(false)==ccw,"Native topology counterclockwise target.");
  if(cw>=0)Check(helper.RotateClockwise()&&table.Angle==table.AngleForIndex(cw)&&table.StopIndex==cw,"Native topology command destination.");
  Console.WriteLine("Validated installed map turntable topology: "+table.id);
 }
}
Console.WriteLine($"PASS: {checks} production turntable-helper assertions. Synthetic graph/controller contracts; live map validation remains required.");