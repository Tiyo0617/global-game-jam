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

    /// <summary>
    /// 已借出（在途）实例登记本。
    /// ⚠️ 防重复归还：Area2D 的 body_entered 在同一物理帧会为每个同时进入的
    /// 物体各派发一次回调 —— 一发子弹扎进重叠的敌群里会收到 N 次"命中"，
    /// 若无脑 Return N 次，同一节点会在 _free 栈里记 N 笔账；之后 Rent 把
    /// 同一个实例借给 N 个"分身"，表现为子弹凭空消失 / 互相吞（P2-19 吞弹 bug）。
    /// Return 时先查是否在途：不在途 = 已还过，直接忽略。
    /// </summary>
    private readonly HashSet<T> _inUse = new();

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
        _inUse.Add(n);
        n.SetPhysicsProcess(true);
        n.Visible = true;
        return n;
    }

    public void Return(T n)
    {
        if (!GodotObject.IsInstanceValid(n)) return;

        // 防重复归还：只有真正在途的实例才允许还池。同一实例第二次 Return 时
        // Remove 返回 false → 忽略，否则会重复入栈导致"一节点多分身"。
        if (!_inUse.Remove(n))
        {
            GD.PushWarning($"[Pool] 重复归还 {typeof(T).Name}，已忽略（防分身 bug）。");
            return;
        }

        // ⚠️ 所有"物理相关状态变更"必须走 deferred。
        //    OnBodyEntered / 出生点 Instantiate 等物理回调或紧邻物理 flush 的时机里，
        //    直接赋值会触发 Godot 警告 "Can't change this state while flushing queries"。
        //    set_deferred / call_deferred 把改动排到当前物理帧结束之后再执行。
        n.CallDeferred("set_physics_process", false);
        n.SetDeferred("visible", false);
        n.SetDeferred("global_position", new Vector2(-99999f, -99999f));
        _free.Push(n);
    }
}
