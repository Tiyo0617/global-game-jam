using Godot;

namespace GGJ;

/// <summary>皮肤类别：决定敌人从哪个贴图池换皮。</summary>
public enum EnemySkinKind
{
    /// <summary>普通怪：从默认池（鸟/虫/甲虫）随机。</summary>
    Normal = 0,
    /// <summary>精英怪（Boss）：从考拉/鸽子大体积池随机。</summary>
    Elite = 1,
    /// <summary>分裂母体：马蜂窝。分裂词条开启后，普通母体生前即用此造型。</summary>
    Hive = 2,
    /// <summary>分裂子怪：马蜂。母体被打死后裂出的 2 个小怪用此造型。</summary>
    Bee = 3,
}

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

    // 普通怪随机换皮：RandomIdleTextures[i] 与 RandomWalkTextures[i] 是同一套
    [Export] public Texture2D?[]? RandomIdleTextures;
    [Export] public Texture2D?[]? RandomWalkTextures;
    [Export] public int RandomIdleFrames = 1;
    [Export] public int RandomWalkFrames = 4;

    // 精英(Boss)专属换皮：从这两池随机，普通怪永不出现
    [Export] public Texture2D?[]? EliteIdleTextures;
    [Export] public Texture2D?[]? EliteWalkTextures;
    [Export] public int EliteIdleFrames = 1;
    [Export] public int EliteWalkFrames = 4;

    // 分裂母体（马蜂窝）专属：分裂词条开启后的普通母体生前用此造型
    [Export] public Texture2D?[]? HiveIdleTextures;
    [Export] public Texture2D?[]? HiveWalkTextures;
    [Export] public int HiveIdleFrames = 4;
    [Export] public int HiveWalkFrames = 4;

    // 分裂子怪（马蜂）专属：母体死亡裂出的 2 个小怪用此造型
    [Export] public Texture2D?[]? BeeIdleTextures;
    [Export] public Texture2D?[]? BeeWalkTextures;
    [Export] public int BeeIdleFrames = 4;
    [Export] public int BeeWalkFrames = 4;

    private bool _autoSwitch;
    private string _current = "";
    private bool _lastVisible;
    private EnemySkinKind _kind;

    public override void _Ready()
    {
        PickRandomTextureSet();
        Rebuild();

        _autoSwitch = WalkTexture != null && GetParent() is CharacterBody2D;
        _lastVisible = IsVisibleInTree();
    }

    /// <summary>
    /// 按皮肤类别换皮：Normal/Elite/Hive/Bee 各从自己的池随机选一套。
    /// 对象池复用必须每次显式调用（回收→再租出会按上次状态再随机一次，以这里为准覆盖）。
    /// </summary>
    public void ApplySkin(EnemySkinKind kind)
    {
        _kind = kind;
        PickRandomTextureSet();
        Rebuild();
    }

    /// <summary>对象池复用节点时不会再触发 _Ready，这里用可见性变化通知补上：
    /// 敌人被回收到池（父节点 Visible=false）再租出（Visible=true）时，重新随机换皮。
    /// 用 _lastVisible 沿检测，避免进入场景树时的重复随机。</summary>
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

    /// <summary>如果提供了对应池的纹理数组，随机选一套（同 idx）覆盖单纹理字段。</summary>
    private void PickRandomTextureSet()
    {
        // 按皮肤类别取池；池没配（可能 tscn 忘了加）就退回普通池，保证不白屏
        var idles  = Pool(_kind == EnemySkinKind.Elite ? EliteIdleTextures : null,
                          _kind == EnemySkinKind.Hive  ? HiveIdleTextures  : null,
                          _kind == EnemySkinKind.Bee   ? BeeIdleTextures   : null,
                          RandomIdleTextures);
        if (idles == null || idles.Length == 0) return;

        int idx = GD.RandRange(0, idles.Length - 1);
        IdleTexture = idles[idx];
        IdleFrames = FramesFor(_kind, isIdle: true);

        var walks = Pool(_kind == EnemySkinKind.Elite ? EliteWalkTextures : null,
                         _kind == EnemySkinKind.Hive  ? HiveWalkTextures  : null,
                         _kind == EnemySkinKind.Bee   ? BeeWalkTextures   : null,
                         RandomWalkTextures);
        if (walks != null && idx < walks.Length)
        {
            WalkTexture = walks[idx];
            WalkFrames = FramesFor(_kind, isIdle: false);
        }
        else
        {
            WalkTexture = null;   // 上一只残留在对象池里的 walk 不能带到本只
        }
    }

    /// <summary>把"分类专属池"与兜底普通池串起来：只取第一个非空的。</summary>
    private static Texture2D?[]? Pool(Texture2D?[]? elite, Texture2D?[]? hive, Texture2D?[]? bee, Texture2D?[]? normal)
        => elite is { Length: > 0 } ? elite
         : hive  is { Length: > 0 } ? hive
         : bee   is { Length: > 0 } ? bee
         : normal;

    private int FramesFor(EnemySkinKind kind, bool isIdle) => kind switch
    {
        EnemySkinKind.Elite => isIdle ? EliteIdleFrames : EliteWalkFrames,
        EnemySkinKind.Hive  => isIdle ? HiveIdleFrames  : HiveWalkFrames,
        EnemySkinKind.Bee   => isIdle ? BeeIdleFrames   : BeeWalkFrames,
        _                   => isIdle ? RandomIdleFrames : RandomWalkFrames,
    };

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
