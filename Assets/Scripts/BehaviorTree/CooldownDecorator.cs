using System.Collections.Generic;
using UnityEngine;

public class CooldownDecorator : BTNode
{
    private BTNode child;
    private float cooldown;
    private float nextAllowedTime;
    private readonly BTNode[] childArray;

    public CooldownDecorator(
        BTNode child,
        float cooldown)
    {
        this.child = child;
        this.cooldown = cooldown;
        nextAllowedTime = 0f;
        childArray = new[] { child };
    }

    public override IReadOnlyList<BTNode> Children => childArray;

    public override string DebugInfo =>
        Time.time < nextAllowedTime
            ? $"cooldown {nextAllowedTime - Time.time:0.0}s"
            : "ready";

    protected override NodeState OnTick()
    {
        if (Time.time < nextAllowedTime)
        {
            return NodeState.Running;
        }

        NodeState state = child.Tick();

        if (state == NodeState.Success)
        {
            nextAllowedTime =
                Time.time + cooldown;
        }

        return state;
    }
}
