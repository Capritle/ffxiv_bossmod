﻿namespace BossMod
{
    public class StateMachineTreeColumn : StateMachineColumn
    {
        public StateMachine? ControlledSM;

        public StateMachineTreeColumn(Timeline timeline, StateMachineTree tree, StateMachine? controlledSM)
            : base(timeline, tree)
        {
            ControlledSM = controlledSM;
        }

        public override void Update()
        {
            Width = Tree.TotalBranches * PixelsPerBranch;
        }

        public override void Draw()
        {
            foreach (var node in Tree.Nodes.Values)
            {
                DrawNode(node, false, node.State == ControlledSM?.ActiveState ? ControlledSM.TimeSinceTransition : null);
            }
        }
    }
}
