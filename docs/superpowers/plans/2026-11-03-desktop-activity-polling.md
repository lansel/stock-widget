# 桌面活动感知轮询 实施计划

> **For agentic workers:** 本环境没有可写代码的子代理，执行时按 native 方式逐任务进行，步骤用复选框跟踪。

**Goal:** 只在回到桌面时持续拉行情；离开桌面、锁屏或空闲 10 分钟后停止请求，回来立即刷新，暂停期间保留最后数据。

**Architecture:** 新增纯策略类 `PollPolicy` 和 `IdleClock`（`GetLastInputInfo` P/Invoke）；在现有的 250ms 桌面状态轮询里集中启停行情计时器，不再在构造和 `Show` 时无条件启动。

**Tech Stack:** C# 5 / .NET Framework 4.x、WinForms、系统自带 csc、现有 SelfTest。

**Spec:** `docs/2026-10-30-desktop-stock-widget-design.md`（本次新增“增补 6”），需求来自 2026-11-03 对话。

## 先说结论（短设计）

- **默认模式**（`hideOnWinD=false`）：前台是桌面或任务栏、存在前台窗口、空闲小于 10 分钟时持续轮询；否则停止。开盘 3 秒，其它时段 60 秒。
- **回到桌面**：立即拉一次，再恢复计时器。
- **锁屏**：`GetForegroundWindow()` 返回 0 即视为离开，不加 SessionSwitch/WTS 钩子；空闲 10 分钟兜底。
- **空闲**：`GetLastInputInfo` 读系统最后输入时间；阈值固定 10 分钟，不加配置项。
- **`hideOnWinD=true`**：挂件只在应用前台时可见，若仍按“桌面才轮询”会永远冻结，所以该模式“可见即轮询”，但仍受无前台窗口和空闲约束。
- **暂停期间**：保留最后数据，不显示橙色陈旧点，也不加“已暂停”角标。
- **启动**：同样走门控，桌面在前台才首次拉取；保存配置触发的一次性刷新保留（算用户主动操作）。
- **休市日**：在桌面时仍 60 秒刷新，离开则不请求。

## 考虑过的方案

- **A. 前台门控启停（选定）**：复用现成的 `GetForegroundWindow` 判断，直接停计时器。改动小，真正实现“离开不请求”。代价是默认模式下挂件若仍露在应用窗口外，会显示暂停前数据。
- **B. 离开时降到 60 秒慢轮询**：数据更新鲜，但仍会产生请求，和“否则不调用”的要求冲突，否决。
- **C. 监听窗口遮挡/可见性**：能判断“是否真被盖住”，但 Win32 状态复杂、易误判，收益不值，否决。

## 全局约束

- 继续用 C# 5：不用字符串插值、`?.`、expression-bodied。
- 不加第三方依赖，只 P/Invoke `user32.dll`。
- 默认模式和 `hideOnWinD=true` 都必须可用；暂停不等于网络异常。
- 构建和自测唯一入口：`powershell -ExecutionPolicy Bypass -File build.ps1`。
- 不自动 commit，等最终确认。

## Review Focus

1. `hideOnWinD=true` 时挂件只在应用前台显示——必须保持轮询，否则永远冻结。
2. 锁屏/安全桌面时前台窗口为 0——即使 hide 模式也不能继续轮询。
3. `Environment.TickCount` 约 49.7 天回绕——空闲计算不能算错。
4. 空闲正好 600000ms——按“离开”处理。
5. 启动时应用在前台、或挂件隐藏——不能偷偷拉数据；门控打开后才首次拉取。

## 文件结构

- 新增 `src/PollPolicy.cs`：纯判定逻辑和空闲差值。
- 新增 `src/IdleClock.cs`：`GetLastInputInfo` 封装。
- 修改 `src/WidgetForm.cs`：计时器所有权和启停。
- 修改 `selftest/SelfTest.cs`：5 个新用例。
- 修改 `build.ps1`：自测源文件加入 `PollPolicy.cs`。
- 修改 `README.md`、`src/AssemblyInfo.cs`、设计文档：说明与版本。

---

### Task 1: 轮询判定策略 + 自测

**Files:**
- Create: `src/PollPolicy.cs`
- Modify: `selftest/SelfTest.cs`（插在 DesktopShowState 用例后、QuoteParser 用例前）
- Modify: `build.ps1:6`（自测源文件列表）

**Interfaces:**
- Consumes: `ForegroundKind`（`src/DesktopShowState.cs`）
- Produces:
  - `public const uint PollPolicy.IdleThresholdMs = 600000;`
  - `public static bool PollPolicy.ShouldPoll(bool widgetVisible, bool hideOnWinD, bool hasForeground, ForegroundKind foreground, uint idleMs)`
  - `public static uint PollPolicy.GetIdleMs(uint lastInputTick, uint nowTick)`

- [x] **Step 1: 写失败测试**

```csharp
fail += Run("PollPolicy default foreground gating", () => {
    Assert(PollPolicy.ShouldPoll(true, false, true, ForegroundKind.Desktop, 0));
    Assert(PollPolicy.ShouldPoll(true, false, true, ForegroundKind.Taskbar, 0));
    Assert(!PollPolicy.ShouldPoll(true, false, true, ForegroundKind.App, 0));
    Assert(!PollPolicy.ShouldPoll(true, false, false, ForegroundKind.Desktop, 0));
});
fail += Run("PollPolicy hidden widget", () => {
    Assert(!PollPolicy.ShouldPoll(false, false, true, ForegroundKind.Desktop, 0));
    Assert(!PollPolicy.ShouldPoll(false, true, true, ForegroundKind.App, 0));
});
fail += Run("PollPolicy idle boundary", () => {
    Assert(PollPolicy.ShouldPoll(true, false, true, ForegroundKind.Desktop, PollPolicy.IdleThresholdMs - 1));
    Assert(!PollPolicy.ShouldPoll(true, false, true, ForegroundKind.Desktop, PollPolicy.IdleThresholdMs));
});
fail += Run("PollPolicy hideOnWinD visible over app", () => {
    Assert(PollPolicy.ShouldPoll(true, true, true, ForegroundKind.App, 0));
    Assert(!PollPolicy.ShouldPoll(true, true, false, ForegroundKind.App, 0));
    Assert(!PollPolicy.ShouldPoll(true, true, true, ForegroundKind.App, PollPolicy.IdleThresholdMs));
});
fail += Run("PollPolicy idle ticks wrap", () => {
    Assert(PollPolicy.GetIdleMs(1000u, 1500u) == 500u);
    Assert(PollPolicy.GetIdleMs(uint.MaxValue - 100u, 100u) == 201u);
});
```

- [x] **Step 2: 跑构建确认失败**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 自测编译失败，提示找不到 `PollPolicy`。

- [x] **Step 3: 实现 `PollPolicy`**

逻辑：

- `ShouldPoll`：可见、有前台窗口、`idleMs < IdleThresholdMs` 三者不满足即 false；`hideOnWinD=true` 时返回 true；否则仅 `Desktop`/`Taskbar` 返回 true。
- `GetIdleMs`：`unchecked(nowTick - lastInputTick)`，处理 32 位回绕。
- 阈值 `600000u`。

- [x] **Step 4: 跑构建确认通过**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 54 项 PASS，`BUILD OK`。

---

### Task 2: 空闲读取 + WidgetForm 启停

**Files:**
- Create: `src/IdleClock.cs`
- Modify: `src/WidgetForm.cs`

**Interfaces:**
- Consumes: `PollPolicy.ShouldPoll`、`PollPolicy.GetIdleMs`、`PollPolicy.IdleThresholdMs`
- Produces: `public static uint IdleClock.GetIdleMs()`

- [x] **Step 1: 新建 `IdleClock`**

`LASTINPUTINFO { uint cbSize; uint dwTime; }`；`GetLastInputInfo` 失败返回 0（按“不空闲”处理，宁可多拉一次）；成功时用 `PollPolicy.GetIdleMs(dwTime, unchecked((uint)Environment.TickCount))`。

- [x] **Step 2: 改 `WidgetForm` 字段和构造**

- 新增字段 `private bool _quotePolling;`
- 删除构造函数里的 `StartQuoteTimer(...)`（`src/WidgetForm.cs:133`），保留初始 `SetMarketClosed`。

- [x] **Step 3: 收拢显示/隐藏**

- `ShowWidget()` 只 `Show()`，不再 `_quoteTimer.Start()` / `FetchOnce()`。
- `HideWidget()` 只 `Hide()`，不再 `_quoteTimer.Stop()`。
- `OnFormShown` 改为：

```csharp
IntPtr fg = GetForegroundWindow();
UpdateQuotePolling(GetForegroundKind(fg), fg != IntPtr.Zero);
```

- [x] **Step 4: 新增 `UpdateQuotePolling`**

```csharp
private void UpdateQuotePolling(ForegroundKind foreground, bool hasForeground)
{
    bool should = PollPolicy.ShouldPoll(
        Visible, _hideOnWinD, hasForeground, foreground, IdleClock.GetIdleMs());
    if (should == _quotePolling) return;
    _quotePolling = should;
    if (!should) { _quoteTimer.Stop(); return; }
    _rows.SetMarketClosed(TradingClock.IsNonTradingDay(DateTime.Now));
    StartQuoteTimer(TradingClock.GetIntervalMs(TradingClock.GetStatus(DateTime.Now)));
    FetchOnce();
}
```

- [x] **Step 5: 接上现有轮询**

- `OnZTick` 末尾调用 `UpdateQuotePolling(kind, fg != IntPtr.Zero);`（放在桌面显示状态决策之后）。
- `OnQuoteTick` 开头加 `if (!_quotePolling) return;`，防止已停止的计时器残留一次 tick。
- `ApplySnapshot` 的配置变更一次性 `FetchOnce()` 保留不动。

- [x] **Step 6: 跑构建**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 54 项 PASS，`BUILD OK`。

- [x] **Step 7: 手动检查（自动化部分已完成；需退出当前运行的旧实例后人工确认界面行为）**

- 默认模式：桌面盯盘时数据按 3 秒变化；切到记事本 10 秒再 Win+D，数据立即刷新。
- `hideOnWinD=true`：桌面上挂件隐藏；打开浏览器挂件出现并持续刷新；Win+D 后隐藏。
- 锁屏再解锁：不崩溃，解锁回桌面立即刷新。
- 暂停期间橙色点不出现。
- 改 `stocks.txt` 仍热生效。

---

### Task 3: 文档、版本、最终验证

**Files:**
- Modify: `README.md`
- Modify: `src/AssemblyInfo.cs`
- Modify: `docs/2026-10-30-desktop-stock-widget-design.md`

- [x] **Step 1: README**

- 功能行改为“回到桌面时交易时段 3 秒刷新；离开桌面暂停请求，回来立即刷新”。
- 已知取舍补充：默认模式下挂件仍露在应用窗口外时显示暂停前数据；`hideOnWinD=true` 时可见即刷新；锁屏/空闲 10 分钟也会暂停。
- 自测数从 49 改为 54。

- [x] **Step 2: 版本**

`AssemblyVersion` / `AssemblyFileVersion` 改为 `1.4.0.0`。

- [x] **Step 3: 设计文档增补 6**

记录 v1.4.0：活动感知轮询、10 分钟空闲、锁屏按无前台处理、hide 模式例外、暂停不置陈旧角标。

- [x] **Step 4: 最终验证**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 54 项 PASS，`BUILD OK`，`bin\StockWidget.exe` 可运行。

- [x] **Step 5: 交给你 review**

运行 `git diff --stat` 和 `git status`，不 commit。

---

## 追加（2026-11-03）：pauseWhenAway 开关

- [x] `WidgetSettings.PauseWhenAway` 默认 `true`；解析 `pauseWhenAway=false` 恢复旧行为
- [x] `PollPolicy.ShouldPoll` 增加 `pauseWhenAway` 参数：`false` 时只要挂件可见就轮询
- [x] `WidgetForm` 应用配置并传入策略
- [x] 新增 2 项自测（配置解析 1 + 旧行为 1），总计 56 项
- [x] README、`stocks.txt.sample`、设计文档增补 6 更新
