﻿using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BossMod
{
    // world state that is updated to correspond to game state
    class WorldStateGame : WorldState, IDisposable
    {
        private Network _network;
        private PartyAlliance _alliance = new();
        private List<Operation> _globalOps = new();
        private Dictionary<ulong, List<Operation>> _actorOps = new();

        public WorldStateGame(Network network)
        {
            _network = network;
            _network.EventActionEffect += OnNetworkActionEffect;
            _network.EventActorControlTargetIcon += OnNetworkActorControlTargetIcon;
            _network.EventActorControlTether += OnNetworkActorControlTether;
            _network.EventActorControlTetherCancel += OnNetworkActorControlTetherCancel;
            _network.EventActorControlSelfDirectorUpdate += OnNetworkActorControlSelfDirectorUpdate;
            _network.EventEnvControl += OnNetworkEnvControl;
            _network.EventWaymark += OnNetworkWaymark;
        }

        public void Dispose()
        {
            _network.EventActionEffect -= OnNetworkActionEffect;
            _network.EventActorControlTargetIcon -= OnNetworkActorControlTargetIcon;
            _network.EventActorControlTether -= OnNetworkActorControlTether;
            _network.EventActorControlTetherCancel -= OnNetworkActorControlTetherCancel;
            _network.EventActorControlSelfDirectorUpdate -= OnNetworkActorControlSelfDirectorUpdate;
            _network.EventEnvControl -= OnNetworkEnvControl;
            _network.EventWaymark -= OnNetworkWaymark;
        }

        public void Update(TimeSpan prevFramePerf)
        {
            Execute(new OpFrameStart() { NewTimestamp = DateTime.Now, PrevUpdateTime = prevFramePerf });
            if (CurrentZone != Service.ClientState.TerritoryType)
            {
                Execute(new OpZoneChange() { Zone = Service.ClientState.TerritoryType });
            }

            foreach (var op in _globalOps)
            {
                Execute(op);
            }
            _globalOps.Clear();

            UpdateActors();
            UpdateParty();
        }

        private void UpdateActors()
        {
            Dictionary<ulong, GameObject> seenIDs = new();
            foreach (var obj in Service.ObjectTable)
                if (obj.ObjectId != GameObject.InvalidGameObjectId)
                    seenIDs[obj.ObjectId] = obj;

            List<Actor> delActors = new();
            foreach (var e in Actors)
                if (!seenIDs.ContainsKey(e.InstanceID))
                    delActors.Add(e);

            foreach (var actor in delActors)
                RemoveActor(actor);

            foreach ((_, var obj) in seenIDs)
                UpdateActor(obj);

            foreach (var (id, ops) in _actorOps)
                Service.Log($"[WorldState] {ops.Count} actor events for unknown entity {id:X}");
            _actorOps.Clear();
        }

        private void RemoveActor(Actor actor)
        {
            DispatchActorEvents(actor.InstanceID);
            Execute(new ActorState.OpDestroy() { InstanceID = actor.InstanceID });
        }

        private void UpdateActor(GameObject obj)
        {
            var character = obj as Character;
            var name = obj.Name.TextValue;
            var classID = (Class)(character?.ClassJob.Id ?? 0);
            var posRot = new Vector4(obj.Position, obj.Rotation);
            var hp = new ActorHP();
            if (character != null)
            {
                hp.Cur = character.CurrentHp;
                hp.Max = character.MaxHp;
                hp.Shield = (uint)(Utils.CharacterShieldValue(character) * 0.01f * hp.Max);
            }
            var targetable = Utils.GameObjectIsTargetable(obj);
            var isDead = Utils.GameObjectIsDead(obj);
            var inCombat = character?.StatusFlags.HasFlag(StatusFlags.InCombat) ?? false;
            var target = SanitizedObjectID(obj.TargetObjectId);

            var act = Actors.Find(obj.ObjectId);
            if (act == null)
            {
                Execute(new ActorState.OpCreate() {
                    InstanceID = obj.ObjectId,
                    OID = obj.DataId,
                    Name = name,
                    Type = (ActorType)(((int)obj.ObjectKind << 8) + obj.SubKind),
                    Class = classID,
                    PosRot = posRot,
                    HitboxRadius = obj.HitboxRadius,
                    HP = hp,
                    IsTargetable = targetable,
                    OwnerID = SanitizedObjectID(obj.OwnerId)
                });
                act = Actors.Find(obj.ObjectId)!;
            }
            else
            {
                if (act.Name != name)
                    Execute(new ActorState.OpRename() { InstanceID = act.InstanceID, Name = name });
                if (act.Class != classID)
                    Execute(new ActorState.OpClassChange() { InstanceID = act.InstanceID, Class = classID });
                if (act.PosRot != posRot)
                    Execute(new ActorState.OpMove() { InstanceID = act.InstanceID, PosRot = posRot });
                if (act.HitboxRadius != obj.HitboxRadius)
                    Execute(new ActorState.OpSizeChange() { InstanceID = act.InstanceID, HitboxRadius = obj.HitboxRadius });
                if (act.HP.Cur != hp.Cur || act.HP.Max != hp.Max || act.HP.Shield != hp.Shield)
                    Execute(new ActorState.OpHP() { InstanceID = act.InstanceID, Value = hp });
                if (act.IsTargetable != targetable)
                    Execute(new ActorState.OpTargetable() { InstanceID = act.InstanceID, Value = targetable });
            }

            if (act.IsDead != isDead)
                Execute(new ActorState.OpDead() { InstanceID = act.InstanceID, Value = isDead });
            if (act.InCombat != inCombat)
                Execute(new ActorState.OpCombat() { InstanceID = act.InstanceID, Value = inCombat });
            if (act.TargetID != target)
                Execute(new ActorState.OpTarget() { InstanceID = act.InstanceID, Value = target });
            DispatchActorEvents(act.InstanceID);

            var chara = obj as BattleChara;
            if (chara != null)
            {
                ActorCastInfo? curCast = chara.IsCasting
                    ? new ActorCastInfo
                    {
                        Action = new((ActionType)chara.CastActionType, chara.CastActionId),
                        TargetID = SanitizedObjectID(chara.CastTargetObjectId),
                        Location = Utils.BattleCharaCastLocation(chara),
                        TotalTime = chara.TotalCastTime,
                        FinishAt = CurrentTime.AddSeconds(Math.Clamp(chara.TotalCastTime - chara.CurrentCastTime, 0, 100000)),
                        Interruptible = chara.IsCastInterruptible
                    } : null;
                UpdateActorCastInfo(act, curCast);

                for (int i = 0; i < chara.StatusList.Length; ++i)
                {
                    // note: sometimes (Ocean Fishing) remaining-time is weird (I assume too large?) and causes exception in AddSeconds - so we just clamp it to some reasonable range
                    // note: self-cast buffs with duration X will have duration -X until EffectResult (~0.6s later); see autorotation for more details
                    ActorStatus curStatus = new();
                    var s = chara.StatusList[i];
                    if (s != null && s.StatusId != 0)
                    {
                        var dur = Math.Min(Math.Abs(s.RemainingTime), 100000);
                        curStatus.ID = s.StatusId;
                        curStatus.SourceID = SanitizedObjectID(s.SourceID);
                        curStatus.Extra = StatusExtra(s);
                        curStatus.ExpireAt = CurrentTime.AddSeconds(dur);
                    }
                    UpdateActorStatus(act, i, curStatus);
                }
            }
        }

        private void UpdateActorCastInfo(Actor act, ActorCastInfo? cast)
        {
            if (cast == null && act.CastInfo == null)
                return; // was not casting and is not casting

            if (cast != null && act.CastInfo != null && cast.Action == act.CastInfo.Action && cast.TargetID == act.CastInfo.TargetID)
            {
                // continuing casting same spell
                act.CastInfo.TotalTime = cast.TotalTime;
                act.CastInfo.FinishAt = cast.FinishAt;
                return;
            }

            // update cast info
            Execute(new ActorState.OpCastInfo() { InstanceID = act.InstanceID, Value = cast });
        }

        private void UpdateActorStatus(Actor act, int index, ActorStatus value)
        {
            // note: some statuses have non-zero remaining time but never tick down (e.g. FC buffs); currently we ignore that fact, to avoid log spam...
            // note: RemainingTime is not monotonously decreasing (I assume because it is really calculated by game and frametime fluctuates...), we ignore 'slight' duration increases (<1 sec)
            var prev = act.Statuses[index];
            if (prev.ID == value.ID && prev.SourceID == value.SourceID && prev.Extra == value.Extra && (value.ExpireAt - prev.ExpireAt).TotalSeconds <= 1)
            {
                act.Statuses[index].ExpireAt = value.ExpireAt;
                return;
            }

            // update status info
            Execute(new ActorState.OpStatus() { InstanceID = act.InstanceID, Index = index, Value = value });
        }

        private unsafe void UpdateParty()
        {
            // update player slot
            UpdatePartySlot(PartyState.PlayerSlot, Service.ClientState.LocalContentId, Service.ClientState.LocalPlayer?.ObjectId ?? 0);

            // update normal party slots: first update/remove existing members, then add new ones
            for (int i = PartyState.PlayerSlot + 1; i < PartyState.MaxPartySize; ++i)
            {
                var contentID = Party.ContentIDs[i];
                if (contentID == 0)
                    continue; // skip empty slots

                var member = _alliance.FindPartyMember(contentID);
                if (member == null)
                    UpdatePartySlot(i, 0, 0);
                else
                    UpdatePartySlot(i, contentID, member->ObjectID);
            }
            for (int i = 0; i < _alliance.NumPartyMembers; ++i)
            {
                var member = _alliance.PartyMember(i);
                if (member == null)
                    continue;

                var contentID = (ulong)member->ContentID;
                if (Party.ContentIDs.IndexOf(contentID) != -1)
                    continue; // already added, updated in previous loop

                var freeSlot = Party.ContentIDs.Slice(1).IndexOf(0ul);
                if (freeSlot == -1)
                {
                    Service.Log($"[WorldState] Failed to find empty slot for party member {contentID:X}:{member->ObjectID:X}");
                    continue;
                }

                UpdatePartySlot(freeSlot + 1, contentID, member->ObjectID);
            }

            // update alliance members
            for (int i = PartyState.MaxPartySize; i < PartyState.MaxAllianceSize; ++i)
            {
                var member = _alliance.IsAlliance && !_alliance.IsSmallGroupAlliance ? _alliance.AllianceMember(i - PartyState.MaxPartySize) : null;
                UpdatePartySlot(i, 0, member != null ? member->ObjectID : 0);
            }
        }

        private void UpdatePartySlot(int slot, ulong contentID, ulong instanceID)
        {
            if (contentID != (slot < PartyState.MaxPartySize ? Party.ContentIDs[slot] : 0) || instanceID != Party.ActorIDs[slot])
                Execute(new PartyState.OpModify() { Slot = slot, ContentID = contentID, InstanceID = instanceID });
        }

        private ushort StatusExtra(Dalamud.Game.ClientState.Statuses.Status s) => (ushort)((s.Param << 8) | s.StackCount);
        private ulong SanitizedObjectID(uint raw) => raw != GameObject.InvalidGameObjectId ? raw : 0;

        private void DispatchActorEvents(ulong instanceID)
        {
            var ops = _actorOps.GetValueOrDefault(instanceID);
            if (ops == null)
                return;

            foreach (var op in ops)
                Execute(op);
            _actorOps.Remove(instanceID);
        }

        private void OnNetworkActionEffect(object? sender, (ulong actorID, ActorCastEvent cast) args)
        {
            _actorOps.GetOrAdd(args.actorID).Add(new ActorState.OpCastEvent() { InstanceID = args.actorID, Value = args.cast });
        }

        private void OnNetworkActorControlTargetIcon(object? sender, (ulong actorID, uint iconID) args)
        {
            _actorOps.GetOrAdd(args.actorID).Add(new ActorState.OpIcon() { InstanceID = args.actorID, IconID = args.iconID });
        }

        private void OnNetworkActorControlTether(object? sender, (ulong actorID, ulong targetID, uint tetherID) args)
        {
            _actorOps.GetOrAdd(args.actorID).Add(new ActorState.OpTether() { InstanceID = args.actorID, Value = new() { ID = args.tetherID, Target = args.targetID } });
        }

        private void OnNetworkActorControlTetherCancel(object? sender, ulong actorID)
        {
            _actorOps.GetOrAdd(actorID).Add(new ActorState.OpTether() { InstanceID = actorID, Value = new() });
        }

        private void OnNetworkActorControlSelfDirectorUpdate(object? sender, (uint directorID, uint updateID, uint p1, uint p2, uint p3, uint p4) args)
        {
            _globalOps.Add(new OpDirectorUpdate() { DirectorID = args.directorID, UpdateID = args.updateID, Param1 = args.p1, Param2 = args.p2, Param3 = args.p3, Param4 = args.p4 });
        }

        private void OnNetworkEnvControl(object? sender, (uint directorID, byte index, uint state) args)
        {
            _globalOps.Add(new OpEnvControl() { DirectorID = args.directorID, Index = args.index, State = args.state });
        }

        private void OnNetworkWaymark(object? sender, (Waymark waymark, Vector3? pos) args)
        {
            _globalOps.Add(new WaymarkState.OpWaymarkChange() { ID = args.waymark, Pos = args.pos });
        }
    }
}
