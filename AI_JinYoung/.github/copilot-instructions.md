# 🚀 AI_JinYoung Workspace Instructions

Welcome to the **AI_JinYoung** Unity project! These instructions are intended to help GitHub Copilot Chat (and other AI agents) become productive quickly in the repository. Follow the conventions and patterns described here when adding features, debugging, or creating new AI modules.

---

## 🧱 Project Overview

This repository contains a Unity-based AI platform comprised of multiple self‑contained modules. Each module implements a specific AI feature such as text filtering, AR drawing recognition, animal classification, self‑training, and Unity ML‑Agents training scenarios.

- The root solution is `AI_JinYoung.slnx`.
- Unity version: **6000.3.9f1** (macOS target).

The core training framework lives under `Assets/5.AI_Unity_MLAgent/`, while other modules are numbered `0` through `4` and store their own assets, scripts, and tests.

---

## 📁 Directory Structure

```
Assets/
  0.Common/                 # Shared utilities (e.g. fonts, helpers)
  1.AI_TextFiltering/       # Bad‑word filter using ONNX inference
  2.AI_ARDrawing/           # Digit recognition for AR drawing
  3.AI_AnimalRecognizer/    # Dog/cat image classifier
  4.AI_SelfTrainRecognizer/ # Multi‑class recognizer
  5.AI_Unity_MLAgent/       # ML‑Agents training scenarios
  ML-Agents/                # Unity ML‑Agents package files
  ...
```

Each numbered module typically contains:
- `Scripts/` folder with C# sources
- `AIModelData/` folder with ONNX or trained models
- Unit tests named `*Test.cs` verifying inference pipelines

---

## ⚙️ Common Conventions

### ML‑Agents Modules

- **Base class**: `CharacterAgent.cs` implements health, stamina, and combat logic. New agents should extend this or `Agent` directly.
- Override core methods:
  - `OnEpisodeBegin()` → reset state
  - `CollectObservations(VectorSensor sensor)` → add observations
  - `OnActionReceived(ActionBuffers actions)` → execute actions and call `AddReward`/`SetReward`
  - `Heuristic(in ActionBuffers actionsOut)` → manual control for debugging
- Reward shaping is critical; use small per‑frame rewards and penalties for undesirable behaviors.
- Observations often include position, velocity, and target vectors; actions combine continuous movement/rotation and discrete command branches.

### Inference Tests

Tests verify that a model loads and produces expected results. They follow this pattern:

```csharp
[Test]
public async Task FilterInputTest()
{
    var model = ModelLoader.Load(modelAsset);
    using var worker = new Worker(model, BackendType.GPUCompute);
    // Prepare input tensor...
    var output = worker.Execute(input); // async/await
    Assert.AreEqual(expected, output); 
}
```

Use `async/await` for inference; name files with `Test` suffix.

### Model Loading Boilerplate

```csharp
public ModelAsset modelAsset;
private Model runtimeModel;
private Worker worker;

void Start()
{
    runtimeModel = ModelLoader.Load(modelAsset);
    worker = new Worker(runtimeModel, BackendType.GPUCompute);
}
```

---

## 🛠 Build & Development

- **Open the project** in Unity Hub or via `AI_JinYoung.slnx` using Visual Studio for Mac/VS Code.
- **Attach debugger** using VS Code's `Attach to Unity` configuration (`.vscode/launch.json`).
- There are no custom CLI build scripts; rely on Unity's standard build pipeline.
- Work with scenes under `Assets/5.AI_Unity_MLAgent/Scene/` for training experiments.

---

## 📦 Dependencies

Check `Packages/manifest.json`. Key packages include:
- `core.unity.ml-agents` v4.0.2
- `com.unity.ai.inference` v2.5.0
- `com.unity.inputsystem` v1.18.0
- `com.unity.test-framework` v1.6.0
- `com.gamelovers.mcp-unity` (git)

---

## ✅ Testing

- Tests are run inside the Unity Editor using the Test Runner.
- Use the existing patterns for asynchronous model tests.

---

## 💡 Tips for Copilot Chat

- When authoring new agents or reward logic, mirror the structure of existing classes in `Assets/5.AI_Unity_MLAgent/Scripts/`.
- Leverage the common model loading and inference helpers found across modules.
- If adding a new module, follow the numeric prefix convention and include a `*Test.cs` file.
- Use comments to explain reward shaping and observation design decisions.

---

> **Note:** No prior workspace-specific instructions were detected; this document serves as the comprehensive baseline. Feel free to propose additional `applyTo` patterns (e.g., per‑module) if the project grows more complex.

---

_End of instructions._
