namespace UnityEngine {
 public class Object {
  public bool Destroyed;
  public static bool operator ==(Object? a,Object? b) { bool an=a is null||a.Destroyed,bn=b is null||b.Destroyed; return an||bn?an==bn:ReferenceEquals(a,b); }
  public static bool operator !=(Object? a,Object? b)=>!(a==b);
  public override bool Equals(object? o)=>ReferenceEquals(this,o);
  public override int GetHashCode()=>System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
 }
 public class MonoBehaviour:Object { public T? GetComponent<T>() where T:class=>null; public Transform transform=new(); }
 public class Transform { public Vector3 position; }
 public class GameObject { public Scene scene=new(); }
 public class Scene { public bool Valid=true; public bool IsValid()=>Valid; }
 public static class Resources {
  public static Object[] Existing=Array.Empty<Object>();
  public static T[] FindObjectsOfTypeAll<T>() where T:Object=>Existing.OfType<T>().ToArray();
 }
 public struct Vector3 { public float x,y,z; }
 public static class Mathf { public static float Repeat(float v,float n)=>v-MathF.Floor(v/n)*n; }
}
namespace Helpers { public static class WorldTransformer { public static UnityEngine.Vector3 GameToWorld(UnityEngine.Vector3 p)=>p; } }
namespace UI.Common { public static class Toast { public static List<string> Messages=new(); public static void Present(string text)=>Messages.Add(text); } }
namespace Track {
 public class TrackNode { public UnityEngine.Vector3 Position; }
 public class TrackSegment { public enum End { A,B } public bool Ephemeral; public bool Available=true; }
 public class Turntable {
  public string id="";
  public List<TrackNode> nodes=new();
  public float Angle;
  public bool IsInitialized=true;
  public int? StopIndex=0;
  public bool Blocked;
  public bool TryGetCarBlockingMovement(out Model.Car? car) { car=null;return Blocked; }
  public Location BridgeLocation(TrackSegment.End end,float distance)=>new("bridge-"+end);
  public Location? PitLocation(int index,float distance)=>new Location("pit-"+index);
  public float AngleForIndex(int i)=>360f/nodes.Count*i;
 }
 public readonly record struct Location(string Key);
 public class TurntableController:UnityEngine.Object {
  public UnityEngine.GameObject gameObject=new(); public bool isActiveAndEnabled=true;
  public Turntable turntable=null!; public float? TargetAngle; public float TargetSpeedPercent;
  private float _speed;
  private bool _wasMoving;
  public float Speed { get=>_speed;set=>_speed=value; }
  public bool WasMoving { get=>_wasMoving;set=>_wasMoving=value; }
  public List<string> Calls=new();
  public void SetAngle(float angle){Calls.Add("angle");turntable.Angle=angle;}
  public void SetStopIndex(int? index){Calls.Add(index.HasValue?"connect":"disconnect");turntable.StopIndex=index;}
 }
 public class Graph {
  public static Graph Shared=new();
  public Dictionary<string,Turntable> Tables=new();
  public Dictionary<string,TurntableController> Controllers=new();
  public Dictionary<TrackNode,List<TrackSegment>> Segments=new();
  public Turntable? TurntableForId(string id)=>Tables.GetValueOrDefault(id);
  public TurntableController? TurntableControllerForId(string id)=>Controllers.GetValueOrDefault(id);
  public IEnumerable<TrackSegment> SegmentsConnectedTo(TrackNode node)=>Segments.GetValueOrDefault(node)??new List<TrackSegment>();
 }
}
namespace Model {
 public class Car {}
 public class TrainController {
  public static TrainController Shared=new();
  public Dictionary<string,Car> Cars=new();
  public Car? CheckForCarAtLocation(Track.Location location)=>Cars.GetValueOrDefault(location.Key);
 }
}
namespace Game.Messages {
 public record TurntableUpdateStopIndex(string TurntableId,long Tick,float Angle,int? StopIndex);
}
namespace Game.State {
 public static class StateManager {
  public static bool IsHost=true;
  public static long Now=1234;
  public static List<Game.Messages.TurntableUpdateStopIndex> Messages=new();
  public static void ApplyLocal(Game.Messages.TurntableUpdateStopIndex message)=>Messages.Add(message);
 }
}