﻿namespace BossMod.Endwalker.Alliance.A3Azeyma
{
    class WardensWarmth : CommonComponents.SpreadFromCastTargets
    {
        public WardensWarmth() : base(ActionID.MakeSpell(AID.WardensWarmthAOE), 6) { }
    }

    class SolarWingsL : CommonComponents.SelfTargetedAOE
    {
        public SolarWingsL() : base(ActionID.MakeSpell(AID.SolarWingsL), new AOEShapeCone(30, 67.5f.Degrees(), 90.Degrees())) { }
    }

    class SolarWingsR : CommonComponents.SelfTargetedAOE
    {
        public SolarWingsR() : base(ActionID.MakeSpell(AID.SolarWingsR), new AOEShapeCone(30, 67.5f.Degrees(), -90.Degrees())) { }
    }

    class FleetingSpark : CommonComponents.SelfTargetedAOE
    {
        public FleetingSpark() : base(ActionID.MakeSpell(AID.FleetingSpark), new AOEShapeCone(60, 135.Degrees())) { }
    }

    class SolarFold : CommonComponents.SelfTargetedAOE
    {
        public SolarFold() : base(ActionID.MakeSpell(AID.SolarFoldAOE), new AOEShapeMulti(new AOEShape[] { new AOEShapeRect(30, 5, 30), new AOEShapeRect(5, 30, 5) })) { }
    }

    class Sunbeam : CommonComponents.SelfTargetedAOE
    {
        public Sunbeam() : base(ActionID.MakeSpell(AID.Sunbeam), new AOEShapeCircle(9), 14) { }
    }

    class SublimeSunset : CommonComponents.Puddles
    {
        public SublimeSunset() : base(ActionID.MakeSpell(AID.SublimeSunsetAOE), 40) { } // TODO: check falloff
    }

    public class A3AzeymaStates : StateMachineBuilder
    {
        public A3AzeymaStates(BossModule module) : base(module)
        {
            // TODO: reconsider
            TrivialPhase()
                .ActivateOnEnter<WardensWarmth>()
                .ActivateOnEnter<SolarWingsL>()
                .ActivateOnEnter<SolarWingsR>()
                .ActivateOnEnter<SolarFlair>()
                .ActivateOnEnter<SolarFans>()
                .ActivateOnEnter<FleetingSpark>()
                .ActivateOnEnter<SolarFold>()
                .ActivateOnEnter<DancingFlame>()
                .ActivateOnEnter<WildfireWard>()
                .ActivateOnEnter<Sunbeam>()
                .ActivateOnEnter<SublimeSunset>();
        }
    }

    // TODO: FarFlungFire mechanic - sometimes (on first cast?) we get visual & stack marker, but no aoe...
    public class A3Azeyma : BossModule
    {
        public A3Azeyma(WorldState ws, Actor primary) : base(ws, primary, new ArenaBoundsCircle(new(-750, -750), 30)) { }
    }
}
