using Godot;

namespace GGJ;

/// <summary>
/// 子弹服务：持有对象池，处理生成 / 回收。由 Main 创建。
/// 其他模块只发事件，不直接碰池子。
/// </summary>
public partial class BulletService : Node
{
    private Pool<Bullet>? _pool;

    public void Init(PackedScene? scene)
    {
        if (scene == null)
        {
            GD.PushWarning("[BulletService] 没有 BulletScene，子弹功能关闭。检查 data/run_config.tres。");
            return;
        }
        _pool = new Pool<Bullet>(scene, this);
        Bus.Sub<SpawnBulletRequest>(this, OnSpawn);
        Bus.Sub<DespawnBullet>(this, OnDespawn);
    }

    private void OnSpawn(SpawnBulletRequest r)
    {
        if (_pool == null) return;
        _pool.Rent().Launch(r.Position, r.Direction, r.Speed, r.Damage, r.Pierce);
    }

    private void OnDespawn(DespawnBullet e)
    {
        if (_pool == null || e.Bullet == null) return;
        if (!GodotObject.IsInstanceValid(e.Bullet)) return;
        _pool.Return(e.Bullet);
    }
}
