using System.Collections.Generic;
using UnityEngine;

public enum NodeState
{
    Success,
    Failure,
    Running
}

public abstract class BTNode
{
    private static readonly BTNode[] NoChildren = new BTNode[0];

    // Data untuk BT Debugger
    public string Name { get; private set; }
    public NodeState LastState { get; private set; }
    public int LastTickFrame { get; private set; } = -1;

    public virtual IReadOnlyList<BTNode> Children => NoChildren;
    public virtual string DebugInfo => "";

    protected BTNode()
    {
        Name = GetType().Name;
    }

    public BTNode Named(string name)
    {
        Name = name;
        return this;
    }

    public NodeState Tick()
    {
        LastState = OnTick();
        LastTickFrame = Time.frameCount;
        return LastState;
    }

    protected abstract NodeState OnTick();
}
