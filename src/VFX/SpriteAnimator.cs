using Godot;

namespace GGJ;

/// <summary>
/// 通用精灵动画控制器。挂在 AnimatedSprite2D 上。
/// - 美术给纹理（可带 hframes 的精灵表），自动切帧建 SpriteFrames。
/// - 父节点是 CharacterBody2D 且有 WalkTexture 时，按速度自动切 idle / walk。
/// - WalkFrames > 1 时按横向精灵表切帧（Rect2）。
/// - 可选 RandomIdleTextures / RandomWalkTextures：每次"生成"随机选一套（同 idx 配对），
///   适合"多种敌人共用一个场景、随机换皮"的场景。对象池复用节点不触发 _Ready，
///   改由 NOTIFICATION_VISIBILITY_CHANGED 通知在"回收→再租出"时重新随机。
/// </summary>
public partial class SpriteAnimator : AnimatedSprite2D
{
    [Export] public Texture2D? IdleTexture;
    [Export] public int IdleFrames = 1;
    [Export] public Texture2D? WalkTexture;
    [Export] public int WalkFrames = 1;
    [Export] public float AnimSpeed = 8f;
    [Export] public bool FlipWithDirection = false;   // 朝向跟随水平移动方向
    [Export] public bool ArtFacesLeft = true;          // 美术默认朝左

    // 随机换皮：RandomIdleTextures[i] 与 RandomWalkTextures[i] 是同一套
    [Export] public Texture2D?[]? RandomIdleTextures;
    [Export] public Texture2D?[]? RandomWalkTextures;
    [Export] public int RandomIdleFrames = 1;
    [Export] public int RandomWalkFrames = 4;

    private bool _autoSwitch;
    private string _current = "";
    private bool _lastVisible;

    public override void _Ready()
    {
        PickRandomTextureSet();
        Rebuild();

        _autoSwitch = WalkTexture != null && GetParent() is CharacterBody2D;
        _lastVisible = IsVisibleInTree();
    }

    /// <summary>
    /// 对象池复用节点时不会再触发 _Ready，这里用可见性变化通知补上：
    /// 敌人被回收到池（父节点 Visible=false）再租出（Visible=true）时，重新随机换皮。
    /// 用 _lastVisible 沿检测，避免进入场景树时的重复随机。
    /// </summary>
    public override void _Notification(int what)
    {
        base._Notification(what);

        if (what != NotificationVisibilityChanged) return;

        bool vis = IsVisibleInTree();
        if (vis && !_lastVisible)
        {
            PickRandomTextureSet();
            Rebuild();
        }
        _lastVisible = vis;
    }

    /// <summary>如果提供了 RandomIdleTextures/WalkTextures，随机选一套（同 idx）覆盖单纹理字段。</summary>
    private void PickRandomTextureSet()
    {
        if (RandomIdleTextures == null || RandomIdleTextures.Length == 0) return;

        int idx = GD.RandRange(0, RandomIdleTextures.Length - 1);
        IdleTexture = RandomIdleTextures[idx];
        IdleFrames = RandomIdleFrames;

        if (RandomWalkTextures != null && idx < RandomWalkTextures.Length)
        {
            WalkTexture = RandomWalkTextures[idx];
            WalkFrames = RandomWalkFrames;
        }
    }

    /// <summary>用当前 Idle/WalkTexture 重建 SpriteFrames 并切回 idle 动画。</summary>
    private void Rebuild()
    {
        BuildFrames();

        if (SpriteFrames != null && SpriteFrames.HasAnimation("idle"))
        {
            Play("idle");
            _current = "idle";
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateFlip();

        if (!_autoSwitch) return;
        var body = (CharacterBody2D)GetParent();
        string target = body.Velocity.LengthSquared() > 1f ? "walk" : "idle";
        if (target != _current && SpriteFrames != null && SpriteFrames.HasAnimation(target))
        {
            Play(target);
            _current = target;
        }
    }

    /// <summary>朝向跟随水平速度：碰墙反射后速度反向，自动翻转朝向。</summary>
    private void UpdateFlip()
    {
        if (!FlipWithDirection) return;
        var parent = GetParent();
        if (parent is not CharacterBody2D body) return;

        float vx = body.Velocity.X;
        if (Mathf.Abs(vx) < 1f) return;   // 静止不翻转，保持当前朝向

        FlipH = ArtFacesLeft ? (vx > 0f) : (vx < 0f);
    }

    private void BuildFrames()
    {
        var frames = new SpriteFrames();
        bool added = false;
        if (IdleTexture != null) { AddAnimation(frames, "idle", IdleTexture, IdleFrames); added = true; }
        if (WalkTexture != null) { AddAnimation(frames, "walk", WalkTexture, WalkFrames); added = true; }
        if (!added) return;
        SpriteFrames = frames;
    }

    private void AddAnimation(SpriteFrames frames, string name, Texture2D tex, int count)
    {
        frames.AddAnimation(name);
        int n = Mathf.Max(1, count);
        if (n == 1)
        {
            frames.AddFrame(name, tex);
        }
        else
        {
            // 横向精灵表切帧（假设美术按 hframes 横向排列）
            int frameW = tex.GetWidth() / n;
            int frameH = tex.GetHeight();
            for (int i = 0; i < n; i++)
            {
                var atlas = new AtlasTexture
                {
                    Atlas = tex,
                    Region = new Rect2(i * frameW, 0, frameW, frameH),
                };
                frames.AddFrame(name, atlas);
            }
        }
        frames.SetAnimationSpeed(name, AnimSpeed);
        frames.SetAnimationLoop(name, true);
    }
}