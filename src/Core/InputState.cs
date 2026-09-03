using Godot;

namespace GGJ;

/// <summary>
/// 输入读取的唯一入口。直接轮询按键，不依赖 Godot InputMap
/// （省掉 project.godot 配置，0 基础队友不会踩坑）。要支持手柄改这一个文件就够。
/// </summary>
public static class InputState
{
    public static Vector2 MoveAxis()
    {
        float x = 0f, y = 0f;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))  x -= 1f;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) x += 1f;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))    y -= 1f;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))  y += 1f;
        var v = new Vector2(x, y);
        return v.LengthSquared() > 1f ? v.Normalized() : v;
    }

    public static bool FireHeld => Input.IsMouseButtonPressed(MouseButton.Left);
    public static bool DashHeld => Input.IsMouseButtonPressed(MouseButton.Right);
}
