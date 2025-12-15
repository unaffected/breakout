# Breakout

A classic Breakout/Arkanoid game built with C# and Raylib.

## Features

- Classic brick-breaking gameplay
- Two-hit bricks (orange) and one-hit bricks (blue)
- Ball trajectory control based on paddle hit position
- Progressive ball acceleration
- Lives system
- Score tracking

## Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (6.0 or higher)

## Building & Running

```bash
# Restore dependencies
dotnet restore

# Run the game
dotnet run

# Build release version
dotnet build -c Release
```

## Controls

- **A** or **Left Arrow** - Move paddle left
- **D** or **Right Arrow** - Move paddle right
- **Space** - Launch ball / Restart game

## Gameplay

- Break all the bricks to win
- Don't let the ball fall off the bottom of the screen
- You have 3 lives
- Orange bricks require 2 hits (4 points per hit, 10 when destroyed)
- Blue bricks require 1 hit (10 points)
- The ball angle changes based on where it hits the paddle
- Ball speed increases slightly with each paddle hit

## Project Structure

```
breakout/
├── program.cs          # Game source code
├── Breakout.csproj     # Project configuration
├── README.md           # This file
└── .gitignore         # Git ignore rules
```

## Dependencies

- [Raylib-cs](https://github.com/ChrisDill/Raylib-cs) - C# bindings for Raylib

## License

Free to use and modify.

