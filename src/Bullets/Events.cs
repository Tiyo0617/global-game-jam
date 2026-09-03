using Godot;

namespace GGJ;

public struct SpawnBulletRequest
{
    public Vector2 Position;
    public Vector2 Direction;
    public float Speed;
    public int Damage;
    public int Pierce;
}

/// <summary>由 Bullet 自己发出，BulletService 负责回收进池。</summary>
public struct DespawnBullet
{
    public Bullet? Bullet;
}
