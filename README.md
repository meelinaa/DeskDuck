# 🦆 DeskDuck

> A transparent desktop companion for Windows — powered by WinUI 3, RabbitMQ, and a locally running Ollama AI model.

![DeskDuck Banner](DeskDuck/Assets/MovingDuck.gif)

---

## Table of Contents

- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Tech Stack](#tech-stack)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Project Structure](#project-structure)
- [Screenshots](#screenshots)
- [License](#license)

---

## Overview

DeskDuck is a **Windows desktop overlay application** built with **WinUI 3 (Windows App SDK)**. A small animated duck walks autonomously across your screen in a fully transparent, always-on-top, click-through window. It stays out of your way while you work — but it's always there.

Beyond the animation, DeskDuck doubles as a **system monitoring agent**: it connects to a local RabbitMQ broker to receive push notifications about battery level, CPU load, and RAM usage. A built-in weather service fetches current conditions from OpenWeatherMap and displays them as duck-speech notifications. Finally, right-clicking the duck opens an **AI chat window** backed by a locally running [Ollama](https://ollama.com/) language model.

This project was built to explore advanced Windows desktop development topics including Win32 interop, WinUI 3 composition APIs, message broker integration, and local LLM inference.

---

## Features

### 🦆 Animated Desktop Companion
- Transparent, borderless, click-through overlay window — the duck does not block mouse input to applications beneath it.
- Smooth, autonomous movement: the duck picks a random target position, walks there at a configurable speed, waits for a configurable duration, then picks the next destination.
- Dynamic GIF swapping based on movement state: walking left, walking right, idle sitting, or "held" (Pokéball animation during drag).
- **Drag & Drop**: left-click and drag the duck to any position on screen.
- **Context menu**: right-click to access Chat, Settings, and Exit.

<!-- PLACEHOLDER: Add a GIF of the duck walking across the desktop -->
<!-- Example: ![Duck Walking](docs/images/duck_walking.gif) -->

### ⌨️ Global Hotkey
- Press **Ctrl + Alt + Shift + D** from anywhere to instantly teleport the duck back to the center of the primary display.
- Implemented via a Win32 window subclass (`SetWindowSubclass`) so the hotkey is intercepted at the native message level.

### 🔔 RabbitMQ Notification System
- Connects to a local RabbitMQ broker and listens on the `deskduck.notifications` queue.
- Incoming messages are displayed as a speech-bubble overlay on the duck for 30 seconds.
- Uses **manual acknowledgement** with `prefetchCount = 1` to guarantee sequential, non-overlapping notifications.
- The consumer automatically reconnects with a 5-second retry delay if the broker is unavailable.
- Severity levels are colour-coded: 🔴 red for warnings, 🔵 blue for info/weather.

<!-- PLACEHOLDER: Add a screenshot of a notification bubble appearing on the duck -->
<!-- Example: ![Notification Example](docs/images/notification.png) -->

### 📊 System Monitor Publisher
- Background service that periodically samples **battery level**, **CPU usage**, and **RAM usage**.
- Publishes a warning notification to RabbitMQ the first time each metric exceeds its configured threshold, and resets once the metric recovers — preventing spam.
- CPU usage is measured via two consecutive `GetSystemTimes` snapshots (250 ms apart) for accuracy.
- RAM is read via `GlobalMemoryStatusEx` (P/Invoke).
- Battery is read via the WinRT `Battery.AggregateBattery` API.

### 🌤️ Weather Publisher
- Fetches the current weather from the **OpenWeatherMap API** at a configurable interval.
- Automatically detects the user's city via [ip-api.com](http://ip-api.com) when no override is configured.
- Publishes a formatted weather summary notification to RabbitMQ.

### 🤖 AI Chat Window
- Right-click → "Chat with AI" opens a dedicated chat window.
- Powered by a **locally running Ollama LLM** — no data leaves the machine.
- Supports model switching: all locally available Ollama models are listed in a dropdown.
- Full conversation history is sent to the model on every request, preserving context.
- A configurable system prompt gives the duck its personality.
- The duck window docks next to the chat window and follows it if moved.

<!-- PLACEHOLDER: Add a screenshot of the AI chat window -->
<!-- Example: ![AI Chat Window](docs/images/chat_window.png) -->

### ⚙️ Settings Window
- Toggle coordinate display beneath the duck.
- Enable/disable each monitoring service individually.
- Configure check intervals, warning thresholds, and the OpenWeatherMap API key.
- Settings are persisted to `%LocalAppData%\DeskDuck\appsettings.json` so they survive application updates.

<!-- PLACEHOLDER: Add a screenshot of the settings window -->
<!-- Example: ![Settings Window](docs/images/settings_window.png) -->

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    DeskDuck.exe                         │
│                                                         │
│  ┌──────────────┐   ┌──────────────┐   ┌────────────┐  │
│  │  MainWindow  │   │  ChatWindow  │   │ Settings-  │  │
│  │  (Overlay)   │   │  (AI Chat)   │   │  Window    │  │
│  └──────┬───────┘   └──────┬───────┘   └────────────┘  │
│         │                  │                            │
│  ┌──────▼───────┐   ┌──────▼───────┐                   │
│  │ DuckMovement │   │ OllamaChat   │                   │
│  │   Manager    │   │   Service    │◄── Ollama (local)  │
│  └──────────────┘   └──────────────┘                   │
│                                                         │
│  ┌──────────────────────────────────────────────────┐   │
│  │            .NET Generic Host (IHostedService)    │   │
│  │  ┌────────────────────┐  ┌─────────────────────┐ │   │
│  │  │  SystemMonitor-    │  │  WeatherPublisher-  │ │   │
│  │  │  PublisherService  │  │  Service            │ │   │
│  │  └─────────┬──────────┘  └──────────┬──────────┘ │   │
│  │            └─────────────────────────┘            │   │
│  │                    RabbitMqPublisher               │   │
│  └────────────────────────┬─────────────────────────┘   │
│                           │                             │
│  ┌────────────────────────▼─────────────────────────┐   │
│  │         RabbitMQBackgroundService (Consumer)      │   │
│  └────────────────────────┬─────────────────────────┘   │
└───────────────────────────┼─────────────────────────────┘
                            │ AMQP (localhost:5672)
                    ┌───────▼────────┐
                    │   RabbitMQ     │
                    │  (Docker)      │
                    └────────────────┘
```

The publisher services and the consumer run within the **same process** but are decoupled through RabbitMQ. This means publisher services can be swapped out or extended independently, and the notification delivery logic is entirely separate from the monitoring logic.

---

## Tech Stack

| Layer | Technology |
|---|---|
| UI Framework | [WinUI 3 / Windows App SDK](https://learn.microsoft.com/windows/apps/windows-app-sdk/) |
| Language | C# 12 / .NET 8 |
| Message Broker | [RabbitMQ](https://www.rabbitmq.com/) via Docker |
| AMQP Client | [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client) |
| AI Backend | [Ollama](https://ollama.com/) (local LLM inference) |
| Ollama Client | [OllamaSharp](https://github.com/awaescher/OllamaSharp) |
| Weather API | [OpenWeatherMap](https://openweathermap.org/api) |
| Geolocation | [ip-api.com](http://ip-api.com) |
| Dependency Injection | `Microsoft.Extensions.Hosting` / `Microsoft.Extensions.DependencyInjection` |
| Configuration | `Microsoft.Extensions.Configuration` (JSON, hot-reload) |
| Win32 Interop | P/Invoke (`user32.dll`, `kernel32.dll`, `comctl32.dll`) |
| Serialization | `System.Text.Json` (source-generated, AOT-safe) |
| Containerisation | Docker Compose |

---

## Getting Started

### Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10 (1809+) or Windows 11 | Required by WinUI 3 |
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | Build toolchain |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | Runs RabbitMQ |
| [Ollama](https://ollama.com/) | Optional — only needed for AI chat |

### 1. Start the RabbitMQ broker

```bash
cd DockerDuck
docker compose up -d
```

The RabbitMQ management UI is available at [http://localhost:15672](http://localhost:15672) (user: `deskduck`, password: `deskduck`).

### 2. Pull any Ollama model (optional)

```bash
ollama pull llama3.2
```

Any model visible in `ollama list` will appear in the in-app model selector.

### 3. Build and run

Open `DeskDuck.slnx` in Visual Studio 2022 (17.8+) and press **F5**, or build via the CLI:

```bash
dotnet build DeskDuck/DeskDuck.csproj -c Release
```

> **Note:** The app targets `net8.0-windows10.0.22621.0` and requires the Windows App SDK runtime. Visual Studio will prompt you to install it if missing.

---

## Configuration

Settings are stored in two locations:

| File | Purpose |
|---|---|
| `DeskDuck/appsettings.json` | Default/template configuration shipped with the app |
| `%LocalAppData%\DeskDuck\appsettings.json` | User configuration (created on first run, edited via the Settings window) |
| `DeskDuck/config.json` | Duck movement parameters and Ollama connection settings |

### `appsettings.json` reference

```json
{
  "General": {
    "ShowCoordinates": true
  },
  "Publishers": {
    "SystemMonitor": {
      "Enabled": true,
      "CheckIntervalSeconds": 60,
      "BatteryWarningEnabled": true,
      "BatteryWarningThresholdPercent": 20,
      "CpuWarningEnabled": true,
      "CpuWarningThresholdPercent": 85,
      "RamWarningEnabled": true,
      "RamWarningThresholdPercent": 85
    },
    "Weather": {
      "Enabled": true,
      "IntervalMinutes": 30,
      "ApiKey": "<your-openweathermap-api-key>",
      "OverrideCity": ""
    }
  }
}
```

### `config.json` reference

```json
{
  "Speed": 2.0,
  "MinWaitSeconds": 5,
  "MaxWaitSeconds": 15,
  "OllamaUrl": "http://localhost:11434",
  "OllamaModel": "llama3.2:latest",
  "OllamaPromt": "You are DeskDuck, a helpful and slightly quirky desktop assistant."
}
```

---

## Project Structure

DeskDuck uses a **Vertical Slice Architecture** to group all related files (Views, ViewModels, Services, and Models) by feature rather than by technical layer. This maximizes cohesion, minimizes coupling, and makes adding or removing features significantly easier.

```text
DeskDuck/
├── Features/
│   ├── Chat/            # AI chat window, ViewModel, and Ollama integration
│   ├── Messaging/       # RabbitMQ publisher, background consumer, and config
│   ├── Settings/        # Settings UI, ViewModel, and repository logic
│   ├── Shell/           # Main transparent duck overlay and core bindings
│   ├── SystemMonitor/   # System health metrics publisher (CPU, RAM, Battery)
│   └── Weather/         # OpenWeatherMap publisher and options
├── Enums/               # Duck states and triggers
├── Helper/              # WinUI backdrop and window handling
├── Manager/             # Duck movement engine and state machine
├── Messages/            # IMessenger notification payloads
├── Models/              # Cross-feature configuration models
├── appsettings.json     # Default configuration
└── config.json          # Duck movement & Ollama config

DockerDuck/
└── docker-compose.yml   # RabbitMQ service definition
```

---

## Screenshots

### 🦆 Duck Walking on the Desktop

![Duck Walking on the Desktop](DeskDuck/Assets/MovingDuck.gif)

---

### 🔔 Notification Bubble

![Notification Bubble](DeskDuck/Assets/AlertDuck.gif)

---

### 🤖 AI Chat Window

![AI Chat Window](DeskDuck/Assets/KiChatDuck.png)

---

### ⚙️ Settings Window

![Settings – General](DeskDuck/Assets/Settings1.png)

![Settings – Publishers](DeskDuck/Assets/Settings2.png)

![Settings – Weather](DeskDuck/Assets/Settings3.png)

---

## License

This project is provided for portfolio and demonstration purposes.
