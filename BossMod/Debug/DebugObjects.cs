﻿using Dalamud.Game.ClientState.Objects.Types;
using ImGuiNET;
using System;
using System.Text;

namespace BossMod
{
    public class DebugObjects
    {
        private bool _showCrap = false;

        public unsafe void DrawObjectTable()
        {
            ImGui.Checkbox("Show players, minions and mounts", ref _showCrap);

            int i = 0;
            ImGui.BeginTable("objects", 12, ImGuiTableFlags.Resizable);
            ImGui.TableSetupColumn("Index");
            ImGui.TableSetupColumn("Actor");
            ImGui.TableSetupColumn("Kind/Subkind");
            ImGui.TableSetupColumn("Class");
            ImGui.TableSetupColumn("OwnerID");
            ImGui.TableSetupColumn("HP");
            ImGui.TableSetupColumn("Flags");
            ImGui.TableSetupColumn("Position");
            ImGui.TableSetupColumn("Rotation");
            ImGui.TableSetupColumn("Cast");
            ImGui.TableSetupColumn("Render flags");
            ImGui.TableSetupColumn("Draw data");
            ImGui.TableHeadersRow();
            foreach (var obj in Service.ObjectTable)
            {
                int idx = i++;
                bool isCrap = obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Player
                    || obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Companion
                    || obj.ObjectKind == Dalamud.Game.ClientState.Objects.Enums.ObjectKind.MountType;
                if (isCrap && !_showCrap)
                    continue;

                var character = obj as Character;
                var battleChara = obj as BattleChara;
                var internalObj = Utils.GameObjectInternal(obj);
                var internalChara = Utils.BattleCharaInternal(battleChara);
                ImGui.TableNextRow();
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{idx}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(Utils.ObjectString(obj));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(Utils.ObjectKindString(obj));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(character != null ? $"{character.ClassJob.Id} ({(Class)character.ClassJob.Id})" : "---");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{obj.OwnerId:X}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(character != null ? $"{character.CurrentHp}/{character.MaxHp} ({(character != null ? Utils.CharacterShieldValue(character) : 0)})" : "---");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{character?.StatusFlags}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted(Utils.Vec3String(obj.Position));
                ImGui.TableNextColumn(); ImGui.TextUnformatted(obj.Rotation.Radians().ToString());
                ImGui.TableNextColumn(); ImGui.TextUnformatted(battleChara != null ? $"{battleChara.CurrentCastTime:f2}/{battleChara.TotalCastTime:f2}" : "---");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"{internalObj->RenderFlags:X}");
                ImGui.TableNextColumn(); ImGui.TextUnformatted($"0x{(IntPtr)internalObj->DrawObject:X}");
            }
            ImGui.EndTable();
        }

        public static unsafe void DumpObjectTable()
        {
            var res = new StringBuilder("--- object table dump ---");
            foreach (var obj in Service.ObjectTable)
            {
                var internalObj = Utils.GameObjectInternal(obj);
                res.Append($"\nobj {Utils.ObjectString(obj)}, kind={Utils.ObjectKindString(obj)}, pos={Utils.Vec3String(obj.Position)}, rot={obj.Rotation.Radians()}, renderFlags={internalObj->RenderFlags}, vfxScale={internalObj->VfxScale}, targetable={internalObj->GetIsTargetable()}, drawPtr=0x{(IntPtr)internalObj->DrawObject:X}");
                if (internalObj->DrawObject != null)
                {
                    res.Append($", drawPos={Utils.Vec3String(internalObj->DrawObject->Object.Position)}, drawScale={Utils.Vec3String(internalObj->DrawObject->Object.Scale)}");
                }

                var chara = obj as BattleChara;
                if (chara)
                {
                    res.Append($", vfxObj=0x{Utils.ReadField<ulong>(internalObj, 0x1840):X}/0x{Utils.ReadField<ulong>(internalObj, 0x1848):X}");
                    if (chara!.IsCasting)
                    {
                        var target = Service.ObjectTable.SearchById(chara.CastTargetObjectId);
                        var targetString = target ? Utils.ObjectString(target!) : "unknown";
                        res.Append($", castAction={new ActionID((ActionType)chara.CastActionType, chara.CastActionId)}, castTarget={targetString}, castLoc={Utils.Vec3String(Utils.BattleCharaCastLocation(chara))}, castTime={Utils.CastTimeString(chara.CurrentCastTime, chara.TotalCastTime)}");
                    }
                    foreach (var status in chara!.StatusList)
                    {
                        var src = status.SourceObject ? Utils.ObjectString(status.SourceObject!) : "none";
                        res.Append($"\n  status {status.StatusId} '{status.GameData.Name}': param={status.Param}, stacks={status.StackCount}, time={status.RemainingTime:f2}, source={src}");
                    }
                }
            }
            Service.Log(res.ToString());
        }
    }
}
