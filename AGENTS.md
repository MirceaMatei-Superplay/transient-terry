# AGENTS Guidelines for SuperPlay Repositories

This document sets the rules and expectations for **autonomous agents** (and human contributors) working in SuperPlay code‑bases. Follow them strictly; CI treats most of these guidelines as hard gates.

---

## 1. Code Style

### 1.1 Naming

* **camelCase** for local variables and private fields (`_memberVariable` for the latter).
* **PascalCase** for public fields, classes, structs, enums.
* **Interfaces** start with `I` (e.g. `IResourceConverterSystem`).
* **Constants**: `ALL_CAPS_WITH_UNDERSCORE`.
* Boolean names must read as questions: `isEnabled`, `hasItems`, `areEqual`.

### 1.2 Functions

* Names start with a **verb** and clearly describe behaviour (`PlaySound`, `TryGetValueAndAppend`).
* Use expression‑bodied members for *truly* trivial one‑liners.

### 1.3 Formatting

* Single‑line `if` statements **omit braces**.
* Lines ≤ **120columns**.
* Opening braces on a **new line**.
* Group related logic with blank lines.
* Never use if(!condition) use if(condition == false) instead.

### 1.4 Comments

Write comments only to clarify *why* something exists, not *what* obvious code does.

### 1.5 UniTask & Cancellation

* **Every** `UniTask` call passes a `CancellationToken`.
* Fire‑and‑forget tasks must have `.SuppressCancellationThrow()`.

### 1.6 Modifiers & Data Types

* Constructor‑injected fields are `readonly`.
* Prefer **structs** for mostly immutable payloads.
* Return multiple values with **tuples**, not `out`/`ref`.

### 1.7 Folders & Namespaces

Folder layout mirrors namespaces; declare the namespace immediately after `using` directives.

---

## 2. Build Rules

* **Warnings are errors.** The build must be 100% warning‑free.
* For third‑party code emitting warnings, prefer (in order): replace → patch & fix → suppress locally.

---

## 6. Pull‑Request Guidelines

* **Title:** `<scope>: <summary>` (e.g. `TAG: Fix token cancellation bug`).
* Every PR must be small, self‑contained, and keep the build green.
* No fancy emojis like 🔧

---

## 7. Miscellaneous

* One public type per file.
* UTF‑8 (no BOM), LF line endings.
* Honour existing `.editorconfig`, `.gitattributes`, and pipeline scripts.

---
