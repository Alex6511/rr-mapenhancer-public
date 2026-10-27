using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Messages;
using Game.State;
using Model;
using Helpers;
using Track;
using UnityEngine;

namespace MapEnhancer
{
    // Map commands snap between connected stops through the native controller.
    public sealed class TurntableHelper : MonoBehaviour
    {
        private static readonly FieldInfo? SpeedField = typeof(TurntableController).GetField(
            "_speed", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo? WasMovingField = typeof(TurntableController).GetField(
            "_wasMoving", BindingFlags.Instance | BindingFlags.NonPublic);
        private TurntableController? controller;
        private Turntable? table;
        private int? previousStop;
        public Action? OnRotationComplete;
        public TurntableController? Controller => ResolveController();
        public Turntable? Table => table ?? controller?.turntable;
        public bool IsInitialized => Table != null && Graph.Shared != null &&
            ReferenceEquals(Graph.Shared.TurntableForId(Table.id), Table);

        public void Bind(Turntable value)
        {
            table = value;
            controller = null;
            previousStop = null;
        }

        private TurntableController? ResolveController()
        {
            if (Table == null || Graph.Shared == null) return null;
            if (controller == null || !controller.isActiveAndEnabled ||
                !ReferenceEquals(controller.turntable, Table))
                controller = Graph.Shared.TurntableControllerForId(Table.id);
            // Runtime pack instances may carry DontSave flags and therefore be
            // excluded from Object.FindObjectsByType. Asset prefabs are not
            // command targets: require an enabled instance in a valid scene.
            if (controller == null || !controller.isActiveAndEnabled ||
                !ReferenceEquals(controller.turntable, Table))
                controller = Resources.FindObjectsOfTypeAll<TurntableController>()
                    .FirstOrDefault(candidate => candidate != null && candidate.isActiveAndEnabled &&
                        candidate.gameObject.scene.IsValid() &&
                        ReferenceEquals(candidate.turntable, Table));
            return controller != null && controller.isActiveAndEnabled &&
                ReferenceEquals(controller.turntable, Table) ? controller : null;
        }
        public List<int> ActiveTrackIndexes
        {
            get
            {
                var result = new List<int>();
                if (!EnsureInitialized()) return result;
                var table = Table!;
                for (var i = 0; i < table.nodes.Count; i++)
                {
                    var other = (i + table.nodes.Count / 2) % table.nodes.Count;
                    if (Graph.Shared.SegmentsConnectedTo(table.nodes[i]).Any(s => !s.Ephemeral && s.Available) ||
                        Graph.Shared.SegmentsConnectedTo(table.nodes[other]).Any(s => !s.Ephemeral && s.Available))
                        result.Add(i);
                }
                return result;
            }
        }

        public bool EnsureInitialized(bool logFailures = false)
        {
            if (table == null && controller == null) controller = GetComponent<TurntableController>();
            return IsInitialized;
        }

        private void LateUpdate()
        {
            if (!EnsureInitialized()) return;
            var stop = Table!.StopIndex;
            if (stop != previousStop)
            {
                previousStop = stop;
                if (stop.HasValue) OnRotationComplete?.Invoke();
            }
        }

        public bool MoveToIndex(int index)
        {
            if (!StateManager.IsHost || !EnsureInitialized() || !ActiveTrackIndexes.Contains(index)) return false;
            var target = ResolveController();
            if (target == null) return ReportUnavailable();
            var table = Table!;
            if (!table.IsInitialized || SpeedField?.FieldType != typeof(float) ||
                WasMovingField?.FieldType != typeof(bool))
                return ReportFailure("Turntable is not ready: " + table.id);

            // Check before disconnecting the bridge or changing any native state.
            if (table.TryGetCarBlockingMovement(out _) || DestinationIsBlocked(table, index))
                return ReportFailure("Turntable movement blocked by a car at the bridge connection.");

            target.TargetAngle = null;
            target.TargetSpeedPercent = 0f;
            SpeedField.SetValue(target, 0f);
            WasMovingField.SetValue(target, false);
            target.SetStopIndex(null);
            target.SetAngle(table.AngleForIndex(index));
            target.SetStopIndex(index);
            // A snap never enters the transmitter's moving loop; publish the
            // completed stop explicitly so clients and saved state receive it.
            StateManager.ApplyLocal(new TurntableUpdateStopIndex(
                table.id, StateManager.Now, table.Angle, index));
            previousStop = index;
            OnRotationComplete?.Invoke();
            return true;
        }

        private static bool DestinationIsBlocked(Turntable table, int index)
        {
            var trains = TrainController.Shared;
            foreach (var end in new[] { TrackSegment.End.A, TrackSegment.End.B })
            {
                var bridgeCar = trains.CheckForCarAtLocation(table.BridgeLocation(end, 1f));
                if (bridgeCar == null) continue;
                var pitIndex = end == TrackSegment.End.A ? index : (index + table.nodes.Count / 2) % table.nodes.Count;
                var pit = table.PitLocation(pitIndex, 1f);
                if (!pit.HasValue) continue;
                var pitCar = trains.CheckForCarAtLocation(pit.Value);
                if (pitCar != null && pitCar != bridgeCar) return true;
            }
            return false;
        }
        public int GetCurrentIndex() => EnsureInitialized() ? Table!.StopIndex ?? -1 : -1;

        public int GetNextTrackIndex(bool clockwise = true)
        {
            if (!EnsureInitialized()) return -1;
            var table = Table!;
            var angle = table.Angle;
            return ActiveTrackIndexes
                .Select(i => new { Index = i, Distance = Mathf.Repeat(
                    clockwise ? table.AngleForIndex(i) - angle : angle - table.AngleForIndex(i), 360f) })
                .Where(x => x.Distance > 0.1f)
                .OrderBy(x => x.Distance)
                .Select(x => x.Index)
                .DefaultIfEmpty(-1).First();
        }

        public bool RotateClockwise() => MoveToIndex(GetNextTrackIndex());
        public bool RotateCounterClockwise() => MoveToIndex(GetNextTrackIndex(false));
        public bool Rotate180()
        {
            var current = GetCurrentIndex();
            return current >= 0 && MoveToIndex((current + Table!.nodes.Count / 2) % Table.nodes.Count);
        }
        public Vector3 GetTrackNodePosition(int index)
        {
            if (!EnsureInitialized() || index < 0 || index >= Table!.nodes.Count)
                return transform.position;
            return WorldTransformer.GameToWorld(Table!.nodes[index].Position);
        }

        private bool ReportUnavailable()
        {
            return ReportFailure("Turntable controller is not available: " + Table?.id);
        }

        private static bool ReportFailure(string message)
        {
            UI.Common.Toast.Present(message);
            return false;
        }

        public string GetTrackIndexLabel(int index) => (index + 1).ToString();
    }
}
