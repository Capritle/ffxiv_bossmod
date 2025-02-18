﻿using System;
using System.Linq;

namespace BossMod.Endwalker.Ultimate.DSW2
{
    // first part of the mechanic - charges + spread + rings
    class P2StrengthOfTheWard1 : BossComponent
    {
        public int NumImpactHits { get; private set; }
        private bool _lightningStormsDone;
        private bool _chargesDone;

        private static float _impactRadiusIncrement = 6;
        private static float _lightningStormRadius = 5;
        private static AOEShapeRect _spiralThrustAOE = new(52, 8);

        public override void AddHints(BossModule module, int slot, Actor actor, TextHints hints, MovementHints? movementHints)
        {
            if (!_lightningStormsDone && module.Raid.WithoutSlot().InRadiusExcluding(actor, _lightningStormRadius).Any())
                hints.Add("Spread!");

            if (!_chargesDone && (InChargeAOE(module, actor, OID.SerVellguine) || InChargeAOE(module, actor, OID.SerPaulecrain) || InChargeAOE(module, actor, OID.SerIgnasse)))
                hints.Add("GTFO from charge aoe!");

            if (NumImpactHits < 5)
            {
                var source = module.Enemies(OID.SerGuerrique).FirstOrDefault();
                if (source != null && actor.Position.InDonut(source.Position, NumImpactHits * _impactRadiusIncrement, (NumImpactHits + 1) * _impactRadiusIncrement))
                    hints.Add("GTFO from aoe!");
            }
        }

        public override void DrawArenaBackground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            if (!_chargesDone)
            {
                DrawCharge(module, OID.SerVellguine);
                DrawCharge(module, OID.SerPaulecrain);
                DrawCharge(module, OID.SerIgnasse);
            }

            if (NumImpactHits < 5)
            {
                var source = module.Enemies(OID.SerGuerrique).FirstOrDefault();
                if (source != null)
                {
                    arena.ZoneDonut(source.Position, NumImpactHits * _impactRadiusIncrement, (NumImpactHits + 1) * _impactRadiusIncrement, ArenaColor.AOE);
                }
            }
        }

        public override void DrawArenaForeground(BossModule module, int pcSlot, Actor pc, MiniArena arena)
        {
            if (!_lightningStormsDone)
            {
                arena.AddCircle(pc.Position, _lightningStormRadius, ArenaColor.Danger);
                foreach (var actor in module.Raid.WithoutSlot().Exclude(pc))
                    arena.Actor(actor, actor.Position.InCircle(pc.Position, _lightningStormRadius) ? ArenaColor.PlayerInteresting : ArenaColor.PlayerGeneric);
            }
        }

        public override void OnEventCast(BossModule module, Actor caster, ActorCastEvent spell)
        {
            switch ((AID)spell.Action.ID)
            {
                case AID.LightningStormAOE:
                    _lightningStormsDone = true;
                    break;
                case AID.SpiralThrust:
                    _chargesDone = true;
                    break;
                case AID.HeavyImpactHit1:
                case AID.HeavyImpactHit2:
                case AID.HeavyImpactHit3:
                case AID.HeavyImpactHit4:
                case AID.HeavyImpactHit5:
                    ++NumImpactHits;
                    break;
            }
        }

        private bool IsKnightInChargePosition(BossModule module, Actor? knight)
        {
            return knight != null && MathF.Abs((knight.Position - module.Bounds.Center).LengthSq() - 23 * 23) < 5;
        }

        private bool InChargeAOE(BossModule module, Actor player, OID knightID)
        {
            var knight = module.Enemies(knightID).FirstOrDefault();
            return IsKnightInChargePosition(module, knight) && _spiralThrustAOE.Check(player.Position, knight);
        }

        private void DrawCharge(BossModule module, OID knightID)
        {
            var knight = module.Enemies(knightID).FirstOrDefault();
            if (IsKnightInChargePosition(module, knight))
                _spiralThrustAOE.Draw(module.Arena, knight);
        }
    }
}
