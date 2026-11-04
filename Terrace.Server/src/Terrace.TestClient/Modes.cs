using Terrace.Shared;

namespace Terrace.TestClient;

/// <summary>patrol: 一定間隔で X 座標を左右に往復させて Move を送り続ける。--attack なら 4 回に 1 回、最寄りの敵を攻撃し、落とし物があれば拾う。</summary>
public static class PatrolMode
{
    private const int AttackDamage = 10;
    private const int AttackEveryTicks = 4;

    public static async Task RunAsync(IGameHub hub, WorldView world, MoveState position, int intervalMs, bool attack, CancellationToken token)
    {
        const float halfWidth = 5f;
        const float step = 1f;
        var left = position.X - halfWidth;
        var right = position.X + halfWidth;
        var direction = 1f;
        var tick = 0;

        Log.Write($"[patrol] x={left:F1}..{right:F1} を {intervalMs} ms 間隔で往復します{(attack ? "(攻撃あり)" : "")}");
        while (!token.IsCancellationRequested)
        {
            tick++;
            position.X += direction * step;
            if (position.X >= right) direction = -1f;
            else if (position.X <= left) direction = 1f;

            position.Facing = direction > 0 ? Facing.Right : Facing.Left;
            position.Motion = MotionState.Walk;
            position.VelocityX = direction * step * 1000f / intervalMs;

            hub.Move(position);
            Log.Write($"[send] Move x={position.X:F1} y={position.Y:F1} state={position.Motion}");

            if (attack && tick % AttackEveryTicks == 0)
            {
                var drop = world.NearestDrop(position.X, position.Y);
                if (drop is not null)
                {
                    hub.Pickup(new PickupRequest { DropId = drop.DropId });
                    Log.Write($"[send] Pickup drop#{drop.DropId} item={drop.ItemId}");
                }

                var target = world.NearestAliveEnemy(position.X, position.Y);
                if (target is not null)
                {
                    hub.Attack(new AttackRequest { EnemyInstanceId = target.InstanceId, Damage = AttackDamage });
                    Log.Write($"[send] Attack enemy#{target.InstanceId} damage={AttackDamage}");
                }
            }

            await Task.Delay(intervalMs, token);
        }
    }
}

/// <summary>manual: 矢印キーで座標を動かし、スペースキーで最寄りの敵に Attack、Z で最寄りの落とし物を Pickup。q で終了。</summary>
public static class ManualMode
{
    private const float Step = 1f;
    private const int Damage = 10;

    public static async Task RunAsync(IGameHub hub, WorldView world, MoveState position, CancellationToken token)
    {
        if (Console.IsInputRedirected)
        {
            Log.Write("[manual] 標準入力がリダイレクトされているためキー入力を受け付けません。Ctrl+C で終了してください");
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return;
        }

        Log.Write("[manual] ←→↑↓: 移動  Space: 最寄りの敵を攻撃  Z: 最寄りの落とし物を拾う  q: 終了");
        while (!token.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(30, token);
                continue;
            }

            var key = Console.ReadKey(intercept: true);
            switch (key.Key)
            {
                case ConsoleKey.LeftArrow:
                    position.X -= Step;
                    position.Facing = Facing.Left;
                    SendMove(hub, position, MotionState.Walk);
                    break;
                case ConsoleKey.RightArrow:
                    position.X += Step;
                    position.Facing = Facing.Right;
                    SendMove(hub, position, MotionState.Walk);
                    break;
                case ConsoleKey.UpArrow:
                    position.Y += Step;
                    SendMove(hub, position, MotionState.Jump);
                    break;
                case ConsoleKey.DownArrow:
                    position.Y -= Step;
                    SendMove(hub, position, MotionState.Ladder);
                    break;
                case ConsoleKey.Spacebar:
                {
                    var target = world.NearestAliveEnemy(position.X, position.Y);
                    if (target is null)
                    {
                        Log.Write("[send] 攻撃できる敵がいません");
                        break;
                    }
                    hub.Attack(new AttackRequest { EnemyInstanceId = target.InstanceId, Damage = Damage });
                    Log.Write($"[send] Attack enemy#{target.InstanceId} damage={Damage}");
                    break;
                }
                case ConsoleKey.Z:
                {
                    var drop = world.NearestDrop(position.X, position.Y);
                    if (drop is null)
                    {
                        Log.Write("[send] 落とし物がありません");
                        break;
                    }
                    hub.Pickup(new PickupRequest { DropId = drop.DropId });
                    Log.Write($"[send] Pickup drop#{drop.DropId} item={drop.ItemId}");
                    break;
                }
                case ConsoleKey.Q:
                    return;
            }
        }
    }

    private static void SendMove(IGameHub hub, MoveState position, MotionState motion)
    {
        position.Motion = motion;
        hub.Move(position);
        Log.Write($"[send] Move x={position.X:F1} y={position.Y:F1} state={position.Motion}");
    }
}
