using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;

namespace Breakout
{
  internal static class Program
  {
    public static void Main()
    {
      new Game().run();
    }
  }

  public sealed class Game
  {
    private readonly World world;

    private const string title = "Breakout";

    private const int screen_width = 960;
    private const int screen_height = 540;

    private const double tick_rate = 60.0;
    private const double tick_delta = 1.0 / tick_rate;

    private const double max_frame_time = 0.25;

    public Game()
    {
      world = new World(screen_width, screen_height);
    }

    public void run()
    {
      Raylib.SetConfigFlags(ConfigFlags.VSyncHint);
      Raylib.InitWindow(screen_width, screen_height, title);
      Raylib.SetTargetFPS(240);

      var accumulator = 0.0;

      while (!Raylib.WindowShouldClose())
      {
        var frame_time = Raylib.GetFrameTime();

        frame_time = (frame_time > max_frame_time) ? (float) max_frame_time : frame_time;

        accumulator += frame_time;

        world.input();

        while (accumulator >= tick_delta)
        {
          world.tick((float)tick_delta);
          accumulator -= tick_delta;
        }

        world.render();
      }

      Raylib.CloseWindow();
    }
  }

  public sealed class World
  {
    private readonly int screen_width;
    private readonly int screen_height;

    private PHASE phase = PHASE.READY;

    private readonly Paddle paddle;
    private readonly Ball ball;
    private readonly Grid bricks;

    private int score = 0;
    private int lives = 3;

    private float acceleration = 0f;
    private bool is_pressed = false;

    public World(int screen_width, int screen_height)
    {
      this.screen_width = screen_width;
      this.screen_height = screen_height;

      paddle = new Paddle(screen_width, screen_height);
      ball = new Ball();
      bricks = new Grid(screen_width);

      reset();
    }

    public void input()
    {
      acceleration = 0f;

      if (Raylib.IsKeyDown(KeyboardKey.Left) || Raylib.IsKeyDown(KeyboardKey.A)) acceleration -= 1f;
      if (Raylib.IsKeyDown(KeyboardKey.Right) || Raylib.IsKeyDown(KeyboardKey.D)) acceleration += 1f;

      is_pressed = Raylib.IsKeyPressed(KeyboardKey.Space);
    }

    public bool game_is_over()
    {
      return phase == PHASE.WON || phase == PHASE.LOST;
    }

    public bool game_is_ready()
    {
      return phase == PHASE.READY;
    }

    public bool game_is_active()
    {
      return phase == PHASE.ACTIVE;
    }

    public void on_game_over()
    {
      if (!is_pressed) return;

      reset();
    }

    public void on_game_active(float delta)
    {
      ball.tick(delta);

      handle_boundary_collision();
      handle_paddle_collision();
      handle_brick_collision();

      if (bricks.is_cleared())
      {
        phase = PHASE.WON;
      }
    }

    public void on_game_ready()
    {
      ball.stick_to_paddle(paddle);

      if (!is_pressed) return;

      phase = PHASE.ACTIVE;

      ball.serve();
    }

    public void tick(float delta)
    {
      if (game_is_over())
      {
        on_game_over();
        return;
      }

      paddle.move(acceleration, delta);
      paddle.clamp(screen_width);

      if (game_is_ready())
      {
        on_game_ready();
        return;
      }

      if (game_is_active())
      {
        on_game_active(delta);
      }
    }

    public void render()
    {
      Raylib.BeginDrawing();
      Raylib.ClearBackground(new Color(16, 18, 22, 255));

      bricks.draw();
      paddle.draw();
      ball.draw();

      draw_hud();
      draw_text();

      Raylib.EndDrawing();
    }

    private void reset()
    {
      score = 0;
      lives = 3;

      bricks.rebuild();
      
      ready();
    }

    private void ready()
    {
      paddle.reset();
      ball.reset();
      ball.stick_to_paddle(paddle);

      phase = PHASE.READY;
    }

    private void handle_life_loss()
    {
      lives--;

      if (lives <= 0) {
        phase = PHASE.LOST;

        return;
      }

      ready();
    }

    private void handle_boundary_collision()
    {
      if (ball.position.X - ball.radius < 0f) {
        ball.position.X = ball.radius;
        ball.velocity.X = MathF.Abs(ball.velocity.X);
      } else if (ball.position.X + ball.radius > screen_width) {
        ball.position.X = screen_width - ball.radius;
        ball.velocity.X = -MathF.Abs(ball.velocity.X);
      }

      if (ball.position.Y - ball.radius < 0f) {
        ball.position.Y = ball.radius;
        ball.velocity.Y = MathF.Abs(ball.velocity.Y);
      }

      if (ball.position.Y - ball.radius <= screen_height) return;

      handle_life_loss();
    }

    private void handle_paddle_collision()
    {
      if (ball.velocity.Y <= 0f) return;

      var ball_collider = ball.collider();
      var paddle_collider = paddle.collider();

      if (!Raylib.CheckCollisionRecs(ball_collider, paddle_collider)) return;

      ball.position.Y = paddle_collider.Y - ball.radius - 0.5f;

      var trajectory = (ball.position.X - paddle_collider.X) / paddle_collider.Width; // 0..1

      if (trajectory < 0f) trajectory = 0f;
      if (trajectory > 1f) trajectory = 1f;

      var angle = lerp(-0.85f, 0.85f, trajectory);
      var direction = new Vector2(MathF.Sin(angle), -MathF.Cos(angle));

      ball.velocity = normalize(direction) * ball.speed;

      ball.speed *= 1.01f;
      ball.velocity = normalize(ball.velocity) * ball.speed;
    }

    private void handle_brick_collision()
    {
      var ball_collider = ball.collider();
      var collision = bricks.find_hit(ball_collider);

      if (!collision.HasValue) return;

      var (index, brick) = collision.Value;

      reflect_ball(brick.collider);

      bricks.damage(index);

      score += bricks.is_alive(index) ? 4 : 10;
    }

    private void reflect_ball(Rectangle aabb)
    {
      var collider = ball.collider();

      var left = (collider.X + collider.Width) - aabb.X;
      var right = (aabb.X + aabb.Width) - collider.X;
      var top = (collider.Y + collider.Height) - aabb.Y;
      var bottom = (aabb.Y + aabb.Height) - collider.Y;

      var min_x = MathF.Min(left, right);
      var min_y = MathF.Min(top, bottom);

      if (min_x < min_y) ball.velocity.X *= -1f;
      else ball.velocity.Y *= -1f;
    }

    private void draw_hud()
    {
      Raylib.DrawText($"Score: {score}", 12, 10, 20, Color.RayWhite);
      Raylib.DrawText($"Lives: {lives}", 160, 10, 20, Color.RayWhite);
    }

    private void draw_text()
    {
      string msg = phase switch
      {
        PHASE.READY => "SPACE to serve",
        PHASE.WON => "YOU WIN — SPACE to restart",
        PHASE.LOST => "GAME OVER — SPACE to restart",
        _ => ""
      };

      if (string.IsNullOrEmpty(msg)) {
        return;
      }

      var font_size = 34;
      var text_width = Raylib.MeasureText(msg, font_size);

      var x = (screen_width - text_width) / 2;
      var y = (screen_height / 2) - (font_size / 2);

      Raylib.DrawText(msg, x + 2, y + 2, font_size, new Color(0, 0, 0, 140));
      Raylib.DrawText(msg, x, y, font_size, Color.RayWhite);
    }

    private static float lerp(float a, float collider, float t)
    {
      return a + (collider - a) * t;
    }

    private static Vector2 normalize(Vector2 v)
    {
      var len = v.Length();

      return len <= 1e-6f ? Vector2.Zero : v / len;
    }
  }

  public enum PHASE { READY, ACTIVE, WON, LOST }

  public sealed class Paddle
  {
    private const float width = 140f;
    private const float height = 18f;
    private const float speed = 720f;

    private readonly int screen_width;
    private readonly int screen_height;

    public Vector2 position;

    public Paddle(int screen_width, int screen_height)
    {
      this.screen_width = screen_width;
      this.screen_height = screen_height;

      position = Vector2.Zero;
    }

    public void reset()
    {
      position.X = (screen_width - width) * 0.5f;
      position.Y = screen_height - 54f;
    }

    public void move(float acceleration, float delta)
    {
      if (acceleration == 0f) return;

      position.X += acceleration * speed * delta;
    }

    public void clamp(int screen_width)
    {
      if (position.X < 0f) {
        position.X = 0f;
      }

      if (position.X + width > screen_width) {
        position.X = screen_width - width;
      }
    }

    public Rectangle collider()
    {
      return new Rectangle(position.X, position.Y, width, height);
    }

    public void draw()
    {
      Raylib.DrawRectangleRec(collider(), new Color(240, 240, 240, 255));
    }
  }

  public sealed class Ball
  {
    public float radius = 7.5f;
    public float speed = 520f;

    public Vector2 position;
    public Vector2 velocity;

    private bool served = false;

    public void reset()
    {
      position = Vector2.Zero;
      velocity = Vector2.Zero;
      speed = 520f;
      served = false;
    }

    public void stick_to_paddle(Paddle paddle)
    {
      var p = paddle.collider();
      position.X = p.X + p.Width * 0.5f;
      position.Y = p.Y - radius - 1f;
    }

    public void serve()
    {
      if (served) return;

      var direction = new Vector2(0.35f, -1f);

      direction = direction / direction.Length();

      velocity = direction * speed;
      served = true;
    }

    public void tick(float delta)
    {
      if (!served) return;

      position += velocity * delta;
    }

    public Rectangle collider()
    {
      var diameter = radius * 2f;

      return new Rectangle(position.X - radius, position.Y - radius, diameter, diameter);
    }

    public void draw()
    {
      Raylib.DrawCircleV(position, radius, new Color(230, 230, 230, 255));
    }
  }

  public sealed class Grid
  {
    private const int rows = 6;
    private const int columns = 12;

    private const float padding = 8f;
    private const float top = 70f;
    private const float height = 26f;

    private readonly int screen_width;

    private readonly List<Brick> bricks = new();

    public Grid(int screen_width)
    {
      this.screen_width = screen_width;
    }

    public void rebuild()
    {
      bricks.Clear();

      var width = ((screen_width - padding * 2f) - (columns - 1) * padding) / columns;

      for (var row = 0; row < rows; row++) {
        for (var column = 0; column < columns; column++) {
          var x = padding + column * (width + padding);
          var y = top + row * (height + padding);

          var health = row < 2 ? 2 : 1;

          bricks.Add(new Brick(new Rectangle(x, y, width, height), health));
        }
      }
    }

    public (int index, Brick brick)? find_hit(Rectangle ball_collider)
    {
      for (var i = 0; i < bricks.Count; i++) {
        var brick = bricks[i];

        if (brick.health <= 0) continue;

        if (Raylib.CheckCollisionRecs(ball_collider, brick.collider)) return (i, brick);
      }

      return null;
    }

    public void damage(int index)
    {
      var brick = bricks[index];

      if (brick.health <= 0) return;

      brick.health--;
      bricks[index] = brick;
    }

    public bool is_alive(int index)
    {
      return bricks[index].health > 0;
    }

    public bool is_cleared()
    {
      for (var i = 0; i < bricks.Count; i++) {
        if (bricks[i].health > 0) return false;
      }

      return true;
    }

    public void draw()
    {
      foreach (var brick in bricks) {
        if (brick.health <= 0) continue;

        var col = brick.health == 2
          ? new Color(255, 180, 80, 255)
          : new Color(90, 170, 255, 255);

        Raylib.DrawRectangleRec(brick.collider, col);
        Raylib.DrawRectangleLinesEx(brick.collider, 1f, new Color(40, 40, 40, 255));
      }
    }

    public struct Brick
    {
      public Rectangle collider;
      public int health;

      public Brick(Rectangle collider, int health)
      {
        this.collider = collider;
        this.health = health;
      }
    }
  }
}
