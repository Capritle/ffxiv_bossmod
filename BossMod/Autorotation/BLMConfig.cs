﻿namespace BossMod
{
    [ConfigDisplay(Parent = typeof(AutorotationConfig))]
    class BLMConfig : ConfigNode
    {
        [PropertyDisplay("Execute optimal rotations on Blizzard1 (ST) or Blizzard2 (AOE)")]
        public bool FullRotation = true;
    }
}
