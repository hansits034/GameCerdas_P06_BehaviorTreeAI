using System;

public class ActionNode : BTNode
{
    private Func<NodeState> action;

    public ActionNode(Func<NodeState> action)
    {
        this.action = action;
        Named(action.Method.Name);
    }

    protected override NodeState OnTick()
    {
        return action();
    }
}