## Quickstart

EasyStateful can be installed via package manager. Window -> Package Manager -> + -> Add package from Git url: https://github.com/ChieVFX/unity-easy-stateful.git?path=Assets/Project

Pulling other branches/commits can be done by appending branch name, eg https://github.com/ChieVFX/unity-easy-stateful.git?path=Assets/Project#r/v0_0_2

You should also install DoTween(free version), as for now this package depends on it for tweening.


# Easy Stateful for Unity

*Effortless state management and animated transitions for Unity GameObjects(especially UI).*

## ✨ Overview

**Easy Stateful for Unity** enables a quick and visual workflow for creating and managing object states via Unity's built-in Animation system. It lets you define visual/object states in editor mode, preview them instantly, and trigger smooth state transitions at runtime with customizable time and easing.

---

## 🔧 Setup & Workflow

### 1. Add the Component

Attach the `StatefulRoot` script to any prefab or GameObject you'd like to animate statefully.

### 2. Enter Work Mode

Click **"Work Mode"** in the inspector. First time per object you’ll be prompted to save a Unity **Animation Clip** —this serves as the backbone for your state definitions (editor-only).

### 3. Record States

Then:

* Unity's **Animation Window** opens automatically.
* It enters **Record Mode** to track your changes and "lock" is applied to it.
* If just created, a default state `"Default"` is created as well.

### 4. Add New States

Click **"New State"** in the `StatefulRoot` component to add more states.

> **Each state is linked to a frame in the animation.**
>
> It is recognized by an **AnimationEvent** on that frame, using the state’s name.

### 5. Save Runtime Data

When you're done setting up keyframes:

* Click **"Runtime Mode"**
* First time, it prompts you to save the generated **ScriptableObject** —this contains runtime data for all your states.

---

## ▶️ Previewing States

Inside `StatefulRoot`, use the **state preview buttons** to switch between states in the editor—no need to enter Play Mode.

---

## 🧠 Using From Code

Trigger transitions at runtime via:

<pre class="overflow-visible!" data-start="1728" data-end="1829"><div class="contain-inline-size rounded-md border-[0.5px] border-token-border-medium relative bg-token-sidebar-surface-primary"><div class="flex items-center text-token-text-secondary px-4 py-2 text-xs font-sans justify-between h-9 bg-token-sidebar-surface-primary dark:bg-token-main-surface-secondary select-none rounded-t-[5px]">csharp</div><div class="sticky top-9"><div class="absolute end-0 bottom-0 flex h-9 items-center pe-2"><div class="bg-token-sidebar-surface-primary text-token-text-secondary dark:bg-token-main-surface-secondary flex items-center rounded-sm px-2 font-sans text-xs"><button class="flex gap-1 items-center select-none px-4 py-1" aria-label="Копировать"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" class="icon-xs"><path fill-rule="evenodd" clip-rule="evenodd" d="M7 5C7 3.34315 8.34315 2 10 2H19C20.6569 2 22 3.34315 22 5V14C22 15.6569 20.6569 17 19 17H17V19C17 20.6569 15.6569 22 14 22H5C3.34315 22 2 20.6569 2 19V10C2 8.34315 3.34315 7 5 7H7V5ZM9 7H14C15.6569 7 17 8.34315 17 10V15H19C19.5523 15 20 14.5523 20 14V5C20 4.44772 19.5523 4 19 4H10C9.44772 4 9 4.44772 9 5V7ZM5 9C4.44772 9 4 9.44772 4 10V19C4 19.5523 4.44772 20 5 20H14C14.5523 20 15 19.5523 15 19V10C15 9.44772 14.5523 9 14 9H5Z" fill="currentColor"></path></svg>Копировать</button><span class="" data-state="closed"><button class="flex items-center gap-1 px-4 py-1 select-none"><svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" class="icon-xs"><path d="M2.5 5.5C4.3 5.2 5.2 4 5.5 2.5C5.8 4 6.7 5.2 8.5 5.5C6.7 5.8 5.8 7 5.5 8.5C5.2 7 4.3 5.8 2.5 5.5Z" fill="currentColor" stroke="currentColor" stroke-linecap="round" stroke-linejoin="round"></path><path d="M5.66282 16.5231L5.18413 19.3952C5.12203 19.7678 5.09098 19.9541 5.14876 20.0888C5.19933 20.2067 5.29328 20.3007 5.41118 20.3512C5.54589 20.409 5.73218 20.378 6.10476 20.3159L8.97693 19.8372C9.72813 19.712 10.1037 19.6494 10.4542 19.521C10.7652 19.407 11.0608 19.2549 11.3343 19.068C11.6425 18.8575 11.9118 18.5882 12.4503 18.0497L20 10.5C21.3807 9.11929 21.3807 6.88071 20 5.5C18.6193 4.11929 16.3807 4.11929 15 5.5L7.45026 13.0497C6.91175 13.5882 6.6425 13.8575 6.43197 14.1657C6.24513 14.4392 6.09299 14.7348 5.97903 15.0458C5.85062 15.3963 5.78802 15.7719 5.66282 16.5231Z" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"></path><path d="M14.5 7L18.5 11" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"></path></svg>Редактировать</button></span></div></div></div><div class="overflow-y-auto p-4" dir="ltr"><code class="whitespace-pre! language-csharp"><span><span>StatefulRoot.TweenToState(</span><span><span class="hljs-built_in">string</span></span><span> stateName, </span><span><span class="hljs-built_in">float</span></span><span>? duration = </span><span><span class="hljs-literal">null</span></span><span>, Ease? ease = </span><span><span class="hljs-literal">null</span></span><span>);
</span></span></code></div></div></pre>

* `duration` and `ease` are optional, as it takes them from global settings/group settings or instance settings, where it can be set by ui designers.

---

## 🌟 Additional Features

### 1. Hierarchical Transition Settings

You can define easing and duration for transitions at three levels:

* **Global** (default for all transitions)
* **Group-level overrides** using `StatefulGroupSettingsData` (a ScriptableObject)
* **Per-instance overrides**
* **Per-call** via `TweenToState` method

### 2. Property-Based Rules

Use property overrides to customize how certain properties transition:

Example use-cases(that are created by default in Global Settings):

* `GameObject.enabled`:
  * **Enabling** : set `enabled = true` at first frame
  * **Disabling** : set `enabled = false` at the last frame
* `Color` transitions:
  * Default to **linear easing** to prevent flickering

Define these rules globally or in group-level settings.

---

## 🎹 Work Mode Hotkeys

* `Alt + 4`: Selects the object currently being worked on
* `Alt + 5`: Fills in missing keyframes by copying from `"Default"` state

  *(Prevents unintended interpolation between keyframes)*

---

## ✅ Summary

With **Easy Stateful for Unity** , you can:

* Build complex stateful object behaviors fast and efficiently
* Preview transitions in-editor
* Trigger smooth transitions with customizable logic at runtime

Perfect for UI, VFX, environment toggles, or anything that needs multiple visual states!
