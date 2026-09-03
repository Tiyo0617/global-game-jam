using System.Collections.Generic;
using Godot;

namespace GGJ;

/// <summary>
/// 极简对象池。子弹 / 敌人走这里，避免每帧 Instantiate 掉帧。
/// 回收时不 Free，只是隐藏 + 移出屏幕 + 停物理帧。
/// </summary>
public sealed class Pool<T> where T : Node2D
{
    private readonly PackedScene _scene;
    private readonly Node _parent;
    private readonly Stack<T> _free = new();

    public Pool(PackedScene scene, Node parent)
    {
        _scene = scene;
        _parent = parent;
    }

    public int FreeCount => _free.Count;

    public T Rent()
    {
        T n;
        if (_free.Count > 0)
        {
            n = _free.Pop();
        }
        else
        {
            n = _scene.Instantiate<T>();
            _parent.AddChild(n);
        }
        n.SetPhysicsProcess(true);
        n.Visible = true;
        return n;
    }

    public void Return(T n)
    {
        if (!GodotObject.IsInstanceValid(n)) return;
        n.SetPhysicsProcess(false);
        n.Visible = false;
        n.GlobalPosition = new Vector2(-99999f, -99999f);
        _free.Push(n);
    }
}
