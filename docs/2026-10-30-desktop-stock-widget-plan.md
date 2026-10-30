# 桌面股票挂件 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建一个零安装的 Windows 桌面层股票挂件 exe，显示 1–3 只 A 股的名称与实时涨跌幅，由桌面 `stocks.txt` 驱动。

**Architecture:** 单个 WinForms exe（系统 csc.exe 编译，C# 5 / .NET Framework 4.8），窗口通过 P/Invoke `SetParent` 挂入 Windows 桌面层 WorkerW（失败则降级普通非置顶窗口）；纯逻辑（解析/时钟/配置）与 UI/Win32 分离，由独立的 SelfTest.exe 做断言自测；数据源为腾讯 `qt.gtimg.cn` 批量行情（GBK），WinForms Timer 按交易状态以 3s/60s 轮询。

**Tech Stack:** C# 5、.NET Framework 4.8（系统自带 csc.exe 4.8.9221）、WinForms、HttpClient、System.Net.Http、Microsoft.Win32 注册表、git

**Spec:** `E:\stock-widget\docs\2026-10-30-desktop-stock-widget-design.md`（计划从 spec 论证，执行者需同时阅读两份文档）

## Global Constraints

- 零安装：成品为单个 `bin\StockWidget.exe`；不得要求用户安装任何运行时（系统自带 csc.exe 与 .NET Framework 除外）
- 常驻内存 < 100MB（最终验证）
- C# 5：禁用字符串插值、`?.`、`out var`、discard、expression-bodied 等 C# 6+ 语法
- 所有 `.cs` 源文件保存为 UTF-8；`build.ps1` 对 csc 传 `-codepage:65001`；测试中的中文一律用 `\uXXXX` 转义
- 数据源：`https://qt.gtimg.cn/q=<symbols逗号分隔>`，响应按 GBK（codepage 936）解码，HTTP 超时 3000ms；无效代码返回 `v_pv_none_match="1";`（无对应 `v_<symbol>` 条目）
- 刷新间隔：`Open=3000`ms，其余状态 `=60000`ms；状态标签固定文案：`开盘中`/`午休`/`已收盘`/`周末`
- `stocks.txt`：路径 = `Environment.GetFolderPath(SpecialFolder.Desktop)\stocks.txt`；一行一个 6 位数字代码，`#` 开头为注释，空行忽略，去重保序，非 6 位数字行忽略，内容开头剥离 `﻿`
- 涨跌颜色：`Pct > 0` → `Color.Red`，`< 0` → `Color.LimeGreen`，`== 0` → `Color.Gray`；`FormatPct` 输出带符号两位小数 + `%`，数值格式化一律 `CultureInfo.InvariantCulture`
- 窗口：`ShowInTaskbar=false`（即 WS_EX_TOOLWINDOW）、`TopMost=false`、`ShowWithoutActivation=true`、`FormBorderStyle=None`；挂 WorkerW 失败或 `--no-desktop-layer` 时保持普通窗口
- 自启：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`，值名 `StockWidget`，值 = 带引号的 exe 全路径，幂等写入
- 单实例：Mutex 名 `Local\StockWidget_SingleInstance`，第二实例弹 MessageBox「股票挂件已在运行」后退出
- 位置持久化：`%APPDATA%\StockWidget\pos.ini`，内容 `X=<int>` / `Y=<int>` 两行；加载后必须 clamp 进当前 `Screen.WorkingArea`
- 拖动结束即保存位置；启动时若无记录则默认 `WorkingArea.Right - Width - 24, WorkingArea.Top + 24`
- 项目根 `E:\stock-widget\`，源码 `src\`，自测 `selftest\`，产物 `bin\`；每任务一次 git commit
- 无节假日日历（spec 已接受的取舍）：工作日处于交易时段即报 `开盘中`

## Review Focus

以下为 spec 隐含、最可能咬人的输入/条件，每条的测试钉在所属任务里：

1. **WorkerW 挂载失败/被系统更新破坏** → 期望：降级为普通窗口，仍显示数据、不进任务栏、不崩溃。测试：Task 6 提供 `--no-desktop-layer` 强制走降级路径做手动验证。
2. **`stocks.txt` 缺失 / 为空 / 含非法行** → 期望：显示提示行，不崩溃。测试：Task 2 单测 `ParseFileContent`（空串、注释、非法行、BOM、重复）；Task 7 手动删文件验证。
3. **断网 / 超时 / 响应为垃圾内容** → 期望：保留上次数据 + 状态栏追加 `网络异常`，不闪退。测试：Task 3 单测 `Parse` 对垃圾输入返回空字典；Task 5 单测 `BuildUrl`/`DecodeGbk`；Task 7 手动断网验证。
4. **时段边界（9:30/11:30/13:00/15:00/周末）** → 期望：状态与间隔精确符合 spec。测试：Task 4 单测全部边界。
5. **显示器变更导致记忆位置越界** → 期望：clamp 回当前工作区，挂件仍可见。测试：Task 8 单测 `PosConfig.Clamp`。

---

### Task 1: 构建骨架 + 自测运行器

**Files:**
- Create: `build.ps1`
- Create: `src/Program.cs`
- Create: `selftest/SelfTest.cs`
- Create: `.gitignore`（内容一行：`bin/`）

**Interfaces:**
- Produces: `build.ps1`（后续所有任务的编译与自测入口）；`SelfTest.Run(string name, Action test) -> int`（0=过 1=败）；`SelfTest.Assert(bool condition) -> void`（失败抛异常）

- [ ] **Step 1: 写 `build.ps1`（后续任务不再改此脚本）**

```powershell
$ErrorActionPreference = "Stop"
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
New-Item -ItemType Directory -Force -Path bin | Out-Null

$selftestSrc = @("selftest\SelfTest.cs")
foreach ($f in @("QuoteParser.cs","StockList.cs","TradingClock.cs","QuoteClient.cs","PosConfig.cs")) {
  $p = "src\$f"
  if (Test-Path $p) { $selftestSrc += $p }
}
& $csc -nologo -codepage:65001 -out:bin\SelfTest.exe -r:System.Net.Http.dll -r:System.Core.dll -r:System.Drawing.dll @selftestSrc
if ($LASTEXITCODE -ne 0) { exit 1 }
& bin\SelfTest.exe
if ($LASTEXITCODE -ne 0) { Write-Host "SELFTEST FAILED"; exit 1 }

$mainSrc = @(Get-ChildItem src\*.cs | ForEach-Object { $_.FullName })
& $csc -nologo -codepage:65001 -out:bin\StockWidget.exe -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Net.Http.dll -r:System.Core.dll $mainSrc
if ($LASTEXITCODE -ne 0) { exit 1 }
Write-Host "BUILD OK"
```

- [ ] **Step 2: 写 `src/Program.cs` 最小入口与 `selftest/SelfTest.cs` 运行器**

`Program.cs`：`static class Program { static int Main(string[] args) { return 0; } }`（无 using 依赖）。

`SelfTest.cs`：`static class SelfTest`，含 `Main`（跑全部用例，打印 `PASS <name>`/`FAIL <name>: <msg>`，全过输出 `ALL PASS` 并 `return 0`，否则 `return 1`）、`Assert(bool)`、`Run(string, Action)`（实现见 Interfaces；`Run` 内 try/catch，异常计失败）。首个用例 `sanity`：`Assert(1 + 1 == 2)`。

- [ ] **Step 3: 运行构建验证**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 输出含 `PASS sanity`、`ALL PASS`、`BUILD OK`，退出码 0

- [ ] **Step 4: git init 并提交**

```bash
git -C E:\stock-widget init
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "chore: build skeleton with selftest runner"
```

---

### Task 2: StockList（stocks.txt 解析 + 市场前缀）

**Files:**
- Create: `src/StockList.cs`
- Modify: `selftest/SelfTest.cs`（追加用例）

**Interfaces:**
- Consumes: `SelfTest.Run` / `SelfTest.Assert`
- Produces:
  - `StockList.ParseFileContent(string content) -> List<string>`（6 位数字代码，去重保序）
  - `StockList.ToSymbol(string code) -> string`（`"600519"→"sh600519"`）
  - `StockList.GetConfigPath() -> string`（桌面 `stocks.txt` 全路径）

- [ ] **Step 1: 写失败用例（追加到 `SelfTest.Main`，调用尚不存在的 `StockList`）**

```csharp
fail += Run("StockList.ParseFileContent basic", () => {
    var r = StockList.ParseFileContent("# c\n\n600519\r\n000001  \n");
    Assert(r.Count == 2 && r[0] == "600519" && r[1] == "000001");
});
fail += Run("StockList.ParseFileContent dedupe and invalid", () => {
    var r = StockList.ParseFileContent("600519\n600519\nabc\n12345\n000001");
    Assert(r.Count == 2 && r[0] == "600519" && r[1] == "000001");
});
fail += Run("StockList.ParseFileContent empty and BOM", () => {
    Assert(StockList.ParseFileContent("").Count == 0);
    Assert(StockList.ParseFileContent("\uFEFF600519").Count == 1);
});
fail += Run("StockList.ToSymbol markets", () => {
    Assert(StockList.ToSymbol("600519") == "sh600519");
    Assert(StockList.ToSymbol("688981") == "sh688981");
    Assert(StockList.ToSymbol("000001") == "sz000001");
    Assert(StockList.ToSymbol("300750") == "sz300750");
    Assert(StockList.ToSymbol("430047") == "bj430047");
    Assert(StockList.ToSymbol("830799") == "bj830799");
    Assert(StockList.ToSymbol("920002") == "bj920002");
    Assert(StockList.ToSymbol("900901") == "sh900901");
});
fail += Run("StockList.GetConfigPath on desktop", () => {
    Assert(StockList.GetConfigPath().EndsWith("stocks.txt"));
});
```

- [ ] **Step 2: 运行验证失败**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 编译错误 CS0246（找不到类型或命名空间 `StockList`），退出码非 0

- [ ] **Step 3: 实现 `src/StockList.cs`**

`public static class StockList`。`ParseFileContent`：先剥离开头 `﻿`，按行拆分，Trim 后跳过空行与 `#` 开头行，行必须满足 `Regex.IsMatch(line, @"^\d{6}$")` 才采纳，用 `HashSet<string>` 保序去重。`ToSymbol`：先判 `code.StartsWith("92")` → `bj`；否则首字符 `6`/`9` → `sh`；`4`/`8` → `bj`；否则 `sz`。`GetConfigPath`：`Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "stocks.txt")`。

- [ ] **Step 4: 运行验证通过**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 新增 5 个 `StockList.*` 用例全 `PASS`，`ALL PASS`、`BUILD OK`

- [ ] **Step 5: Commit**

```bash
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "feat: stocks.txt parser and market symbol mapping"
```

---

### Task 3: QuoteParser（腾讯响应解析 + FormatPct）

**Files:**
- Create: `src/QuoteParser.cs`
- Modify: `selftest/SelfTest.cs`

**Interfaces:**
- Consumes: `SelfTest.Run` / `SelfTest.Assert`
- Produces:
  - `sealed class Quote { public string Code; public string Name; public double Price; public double PrevClose; public double Pct; }`（定义在 `QuoteParser.cs`）
  - `QuoteParser.Parse(string responseText) -> Dictionary<string, Quote>`（key = 6 位代码；忽略 `v_pv_none_match` 及不匹配 `v_(sh|sz|bj)\d+` 的条目；字段不足 33 段的条目跳过）
  - `QuoteParser.FormatPct(double pct) -> string`（`"+1.11%"` / `"-0.61%"` / `"0.00%"`）

- [ ] **Step 1: 写失败用例**

```csharp
static string TEntry(string sym, string code, string name, string price, string pct) {
    var f = new string[33];
    for (int i = 0; i < 33; i++) f[i] = "0";
    f[1] = name; f[2] = code; f[3] = price; f[31] = pct; f[32] = pct;
    return "v_" + sym + "=\"" + string.Join("~", f) + "\"";
}
// 追加到 Main：
fail += Run("QuoteParser.Parse multi entry", () => {
    string text = TEntry("sh600519", "600519", "\u8d35\u5dde\u8305\u53f0", "1369.04", "1.11")
        + ";\n" + TEntry("sz000001", "000001", "\u5e73\u5b89\u94f6\u884c", "11.47", "-0.61")
        + ";\nv_pv_none_match=\"1\";";
    var r = QuoteParser.Parse(text);
    Assert(r.Count == 2);
    Assert(r["600519"].Name == "\u8d35\u5dde\u8305\u53f0");
    Assert(r["600519"].Price == 1369.04 && r["600519"].Pct == 1.11);
    Assert(r["000001"].Pct == -0.61);
});
fail += Run("QuoteParser.Parse garbage and truncated", () => {
    Assert(QuoteParser.Parse("").Count == 0);
    Assert(QuoteParser.Parse("<html>error</html>").Count == 0);
    Assert(QuoteParser.Parse("v_sh600519=\"1~only~two\";").Count == 0);
});
fail += Run("QuoteParser.FormatPct", () => {
    Assert(QuoteParser.FormatPct(1.11) == "+1.11%");
    Assert(QuoteParser.FormatPct(-0.61) == "-0.61%");
    Assert(QuoteParser.FormatPct(0) == "0.00%");
    Assert(QuoteParser.FormatPct(2.5) == "+2.50%");
});
```

- [ ] **Step 2: 运行验证失败**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 编译错误 CS0246（`QuoteParser` 不存在），退出码非 0

- [ ] **Step 3: 实现 `src/QuoteParser.cs`**

`Parse`：`Regex.Matches(text, "v_(?:sh|sz|bj)(\\d+)=\"([^\"]*)\"")`，对每组按 `~` 拆字段，`fields.Length < 33` 则跳过；`Price`/`PrevClose`/`Pct` 用 `double.Parse(fields[i], CultureInfo.InvariantCulture)`（`PrevClose = fields[4]`）；名称取 `fields[1]`，代码取捕获组。`FormatPct`：`pct.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) + "%"`。

- [ ] **Step 4: 运行验证通过**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 新增 3 个用例全 `PASS`，`ALL PASS`、`BUILD OK`

- [ ] **Step 5: Commit**

```bash
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "feat: tencent quote response parser and pct formatter"
```

---

### Task 4: TradingClock（交易状态 + 刷新间隔）

**Files:**
- Create: `src/TradingClock.cs`
- Modify: `selftest/SelfTest.cs`

**Interfaces:**
- Consumes: `SelfTest.Run` / `SelfTest.Assert`
- Produces:
  - `enum MarketStatus { Open, Lunch, Closed, Weekend }`
  - `TradingClock.GetStatus(DateTime now) -> MarketStatus`
  - `TradingClock.GetIntervalMs(MarketStatus status) -> int`（`Open→3000`，其余 `→60000`）
  - `TradingClock.GetLabel(MarketStatus status) -> string`（`开盘中`/`午休`/`已收盘`/`周末`）

- [ ] **Step 1: 写失败用例**

```csharp
fail += Run("TradingClock boundaries", () => {
    var sat = new DateTime(2026, 10, 31, 10, 0, 0);
    var mon = new DateTime(2026, 11, 2, 0, 0, 0); // 周一
    Assert(TradingClock.GetStatus(sat) == MarketStatus.Weekend);
    Assert(TradingClock.GetStatus(mon.AddHours(8)) == MarketStatus.Closed);
    Assert(TradingClock.GetStatus(mon.AddHours(9).AddMinutes(29)) == MarketStatus.Closed);
    Assert(TradingClock.GetStatus(mon.AddHours(9).AddMinutes(30)) == MarketStatus.Open);
    Assert(TradingClock.GetStatus(mon.AddHours(11).AddMinutes(29)) == MarketStatus.Open);
    Assert(TradingClock.GetStatus(mon.AddHours(11).AddMinutes(30)) == MarketStatus.Lunch);
    Assert(TradingClock.GetStatus(mon.AddHours(12).AddMinutes(59)) == MarketStatus.Lunch);
    Assert(TradingClock.GetStatus(mon.AddHours(13)) == MarketStatus.Open);
    Assert(TradingClock.GetStatus(mon.AddHours(14).AddMinutes(59)) == MarketStatus.Open);
    Assert(TradingClock.GetStatus(mon.AddHours(15)) == MarketStatus.Closed);
    Assert(TradingClock.GetStatus(mon.AddHours(16)) == MarketStatus.Closed);
});
fail += Run("TradingClock interval and label", () => {
    Assert(TradingClock.GetIntervalMs(MarketStatus.Open) == 3000);
    Assert(TradingClock.GetIntervalMs(MarketStatus.Lunch) == 60000);
    Assert(TradingClock.GetIntervalMs(MarketStatus.Closed) == 60000);
    Assert(TradingClock.GetIntervalMs(MarketStatus.Weekend) == 60000);
    Assert(TradingClock.GetLabel(MarketStatus.Open) == "\u5f00\u76d8\u4e2d");
    Assert(TradingClock.GetLabel(MarketStatus.Lunch) == "\u5348\u4f11");
    Assert(TradingClock.GetLabel(MarketStatus.Closed) == "\u5df2\u6536\u76d8");
    Assert(TradingClock.GetLabel(MarketStatus.Weekend) == "\u5468\u672b");
});
```

（`\u5f00\u76d8\u4e2d`=开盘中，`\u5348\u4f11`=午休，`\u5df2\u6536\u76d8`=已收盘，`\u5468\u672b`=周末——生产代码 `GetLabel` 内直接写中文字面量，源文件 UTF-8）

- [ ] **Step 2: 运行验证失败**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 编译错误 CS0246（`MarketStatus`/`TradingClock` 不存在），退出码非 0

- [ ] **Step 3: 实现 `src/TradingClock.cs`**

`GetStatus`：`DayOfWeek` 为 Sat/Sun → `Weekend`；否则按本地时间区间判定——`[9:30,11:30)` → `Open`，`[11:30,13:00)` → `Lunch`，`[13:00,15:00)` → `Open`，其余 → `Closed`（半开区间，用 `TimeSpan t = now.TimeOfDay` 与固定 `TimeSpan` 比较）。

- [ ] **Step 4: 运行验证通过**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 新增 2 个用例全 `PASS`，`ALL PASS`、`BUILD OK`

- [ ] **Step 5: Commit**

```bash
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "feat: trading clock with interval and status labels"
```

---

### Task 5: QuoteClient（网络拉取）+ `--fetch` 调试模式

**Files:**
- Create: `src/QuoteClient.cs`
- Modify: `src/Program.cs`（加 `--fetch` 分支）
- Modify: `selftest/SelfTest.cs`

**Interfaces:**
- Consumes: `StockList.ToSymbol`、`QuoteParser.Parse`、`QuoteParser.FormatPct`、`SelfTest.Run/Assert`
- Produces:
  - `QuoteClient.BuildUrl(IEnumerable<string> symbols) -> string`（`https://qt.gtimg.cn/q=sh600519,sz000001`，符号逗号连接）
  - `QuoteClient.DecodeGbk(byte[] bytes) -> string`（`Encoding.GetEncoding(936)`）
  - `QuoteClient.FetchRaw(IEnumerable<string> symbols) -> string`（同步 GET，超时 3000ms，网络失败抛异常）
  - `Program.Main` 新增：`args[0] == "--fetch"` 时对 `args[1..]` 各代码打印一行 `<code> <name> <price> <FormatPct(pct)>`，响应中无该代码则打印 `<code> 无数据`；网络异常打印到 stderr 并 `return 1`，成功 `return 0`

- [ ] **Step 1: 写失败用例**

```csharp
fail += Run("QuoteClient.BuildUrl", () => {
    Assert(QuoteClient.BuildUrl(new[] { "sh600519", "sz000001" }) == "https://qt.gtimg.cn/q=sh600519,sz000001");
});
fail += Run("QuoteClient.DecodeGbk roundtrip", () => {
    string s = "\u8d35\u5dde\u8305\u53f0";
    byte[] bytes = Encoding.GetEncoding(936).GetBytes(s);
    Assert(QuoteClient.DecodeGbk(bytes) == s);
});
```

（`SelfTest.cs` 顶部补 `using System.Text;`）

- [ ] **Step 2: 运行验证失败**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 编译错误 CS0246（`QuoteClient` 不存在），退出码非 0

- [ ] **Step 3: 实现 `src/QuoteClient.cs`**

`public static class QuoteClient`。`BuildUrl`：`"https://qt.gtimg.cn/q=" + string.Join(",", symbols)`。`DecodeGbk`：`Encoding.GetEncoding(936).GetString(bytes)`。`FetchRaw`：静态懒加载 `HttpClient`（`Timeout = TimeSpan.FromMilliseconds(3000)`），`GetByteArrayAsync(BuildUrl(symbols)).Result`（外层调用方会包 `Task.Run`，本方法保持同步签名）后 `DecodeGbk`。

- [ ] **Step 4: 实现 `Program.cs` 的 `--fetch` 分支**

`Main` 开头：`if (args.Length >= 1 && args[0] == "--fetch") return RunFetch(args);`。`RunFetch`：取 `args[1..]`（C# 5 用 for/`Array.Copy` 或 `Skip(1).ToArray()`），映射 `StockList.ToSymbol` → `QuoteClient.FetchRaw` → `QuoteParser.Parse`；逐代码查字典，命中按 Interfaces 格式 `Console.WriteLine`，未命中 `Console.WriteLine(code + " 无数据")`；`catch` 打 `Console.Error.WriteLine("网络异常: " + e.Message)` 并 `return 1`。

- [ ] **Step 5: 运行验证（单测 + 真实网络）**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 单测全 `PASS`、`BUILD OK`

Run: `E:\stock-widget\bin\StockWidget.exe --fetch 600519 000001 999999`
Expected: 前两行形如 `600519 贵州茅台 1369.04 +1.11%`（数值随行情变化），第三行 `999999 无数据`，退出码 0

- [ ] **Step 6: Commit**

```bash
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "feat: quote client with fetch debug mode"
```

---

### Task 6: 桌面层挂载 + WidgetForm 静态壳

**Files:**
- Create: `src/DesktopLayer.cs`
- Create: `src/WidgetForm.cs`
- Modify: `src/Program.cs`（组装窗体、`--no-desktop-layer` 开关）

**Interfaces:**
- Consumes: 无（纯 Win32/UI）
- Produces:
  - `DesktopLayer.Attach(IntPtr hwnd) -> bool`（true=已挂入 WorkerW；false=失败，调用方保持普通窗口）
  - `WidgetForm : Form`，`public WidgetForm(bool attachDesktopLayer)`
  - `Program.Main`：无 `--fetch` 时进入窗体路径；`--no-desktop-layer` 标志 → 传 `false`；否则传 `true`

- [ ] **Step 1: 实现 `src/DesktopLayer.cs`**

`public static class DesktopLayer`。P/Invoke（`DllImport("user32.dll")`）：`FindWindow`、`FindWindowEx`、`SendMessageTimeout`（`SMTO_NORMAL=0`，超时 1000）、`EnumWindows`（委托 `EnumWindowsProc(IntPtr, IntPtr) -> bool`）、`SetParent`。`Attach` 流程：`FindWindow("Progman", null)` 取句柄 → `SendMessageTimeout(progman, 0x052C, IntPtr.Zero, IntPtr.Zero, SMTO_NORMAL, 1000, out result)` → `EnumWindows` 枚举找含子窗口 `SHELLDLL_DefView` 的顶层窗，再取其兄弟 `WorkerW`（`FindWindowEx(IntPtr.Zero, hWnd, "WorkerW", null)`）作为目标 → 找到则 `SetParent(hwnd, target)` 返回 `true`，任一步句柄为零返回 `false`。注意 C# 5：`out` 需预声明变量，不可 `out _`。

- [ ] **Step 2: 实现 `src/WidgetForm.cs` 静态壳**

`public sealed class WidgetForm : Form` 构造：`FormBorderStyle = None`、`ShowInTaskbar = false`、`TopMost = false`、`StartPosition = Manual`、`BackColor = Color.FromArgb(32, 32, 36)`、`Size = new Size(190, 110)`、位置 = `WorkingArea.Right - Width - 24, WorkingArea.Top + 24`；覆写 `ShowWithoutActivation` 返回 `true`。内容：两个占位行 `Label`（文本 `示例股票  +0.00%`、`测试股票  -1.23%`，`ForeColor = Color.WhiteSmoke`，字体 `Microsoft YaHei UI, 10f`）+ 底部状态 `Label`（文本 `测试壳`，`ForeColor = Color.FromArgb(180,180,180)`，字体 8f）。`OnLoad`：`if (attachDesktopLayer) DesktopLayer.Attach(this.Handle);`（返回 false 静默走降级——普通窗口行为已由构造设置保证）。

- [ ] **Step 3: 组装 `Program.cs` 窗体路径**

`Main`：`--fetch` 分支之后，检测 `Array.IndexOf(args, "--no-desktop-layer") >= 0`，`Application.EnableVisualStyles()`，`Application.Run(new WidgetForm(!noLayer))`，`return 0`。

- [ ] **Step 4: 构建 + 手动验证（含降级路径）**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1` → Expected: `BUILD OK`

Run: `E:\stock-widget\bin\StockWidget.exe` → 手动核对：
- 右上角出现深色小窗，两行占位文字 + `测试壳`
- `Win+D` 显示桌面时挂件仍在（桌面层生效）
- 任务栏无按钮、Alt+Tab 无条目
- 打开记事本盖住挂件区域：记事本在上层（挂件不压窗口）
- 不抢焦点（启动后当前应用仍保持焦点）

Run: `E:\stock-widget\bin\StockWidget.exe --no-desktop-layer` → 手动核对：窗口行为降级（Win+D 时一起隐藏），但仍显示、仍不进任务栏、不崩溃

- [ ] **Step 5: Commit**

```bash
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "feat: desktop workerw layer attach and widget form shell"
```

---

### Task 7: 实时数据环路（定时刷新 + txt 热加载 + 状态/异常）

**Files:**
- Modify: `src/WidgetForm.cs`（占位壳 → 实时环路）
- （无新增文件；消费 Task 2/3/4/5 的全部接口）

**Interfaces:**
- Consumes: `StockList.ParseFileContent/GetConfigPath`、`QuoteClient.FetchRaw`、`QuoteParser.Parse/FormatPct/Quote`、`TradingClock.GetStatus/GetIntervalMs/GetLabel`
- Produces: `WidgetForm` 的运行时行为（供 Task 8/9 沿用）——启动即取一次；每 tick 重读 txt、后台拉取、渲染行/状态；`Quote` 字段与颜色规则见 Global Constraints

- [ ] **Step 1: 实现数据环路（替换 `WidgetForm` 占位内容）**

行为契约（实现细节自定，须满足全部条目）：
- 字段：`List<string> _codes`、`Dictionary<string, Quote> _quotes`、`bool _networkError`、`bool _busy`、`System.Windows.Forms.Timer _timer`、行 `Label` 对（name/pct）列表、状态 `Label`
- `ReloadCodes()`：`try { File.ReadAllText(StockList.GetConfigPath()) }` 失败或 `ParseFileContent` 结果为空 → `_codes` 置空并渲染一行提示 `请编辑桌面 stocks.txt（一行一个股票代码）`（灰字，占两列宽）
- `_codes` 与上次一致 → 仅更新各 pct 文本与颜色（避免闪烁）；不一致 → 重建行（行高 26，name 宽 96 左对齐，pct 宽 74 右对齐，字体 `Microsoft YaHei UI 10f`）
- tick（`async void` + `_busy` 防重入，C# 5 async 合法）：`ReloadCodes()` → 有代码则 `await Task.Run(() => QuoteClient.FetchRaw(symbols))`（`symbols = _codes.Select(StockList.ToSymbol)`）→ `QuoteParser.Parse` 成功则更新 `_quotes`、`_networkError=false`，解析结果缺失的代码行显示 name=代码、pct=`无数据`、灰；`catch` → `_networkError=true` 保留旧 `_quotes`
- 渲染：pct 文本 = `QuoteParser.FormatPct(q.Pct)`，颜色按 Global Constraints；状态 `Label` 文本 = `TradingClock.GetLabel(GetStatus(DateTime.Now))` +（`_networkError` ? ` · 网络异常`（`ForeColor=Color.Orange`）: 灰字)
- 每 tick 末尾 `_timer.Interval = TradingClock.GetIntervalMs(status)`；构造末尾 `_timer = new Timer { Interval = TradingClock.GetIntervalMs(...) }`，`Tick += ...`，`Start()` 并立即手动触发一次 handler（启动即取）
- 无代码时也照常走状态刷新（状态 label 仍更新），不发网络请求

- [ ] **Step 2: 构建 + 手动验证**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1` → Expected: `BUILD OK`（既有单测全过）

手动核对（运行 `bin\StockWidget.exe`）：
- 首次启动即显示行情（不等 3 秒）；盘中每几秒数字跳动
- 桌面新建 `stocks.txt` 写入 `600519` 保存 → 下个周期内出现该行；改成两行代码 → 行数变化；清空/删除文件 → 显示提示行，不崩溃
- 状态角标与当前时段一致（工作日白天=`开盘中`，15:00 后=`已收盘`，周末=`周末`）
- 断网（禁用网卡 10 秒）→ 角标出现 `网络异常`、旧数字保留；恢复网络 → 角标消失、数字恢复更新
- `--fetch` 模式回归：`bin\StockWidget.exe --fetch 600519` 仍正常

- [ ] **Step 3: Commit**

```bash
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "feat: live quote loop with txt hot reload and status badge"
```

---

### Task 8: 拖动 + 位置持久化 + 圆角半透明样式

**Files:**
- Create: `src/PosConfig.cs`
- Modify: `src/WidgetForm.cs`（拖动、Region、Opacity、位置加载/保存）
- Modify: `selftest/SelfTest.cs`

**Interfaces:**
- Consumes: `SelfTest.Run/Assert`
- Produces:
  - `PosConfig.Load(string path) -> Point`（文件缺失/损坏 → `Point.Empty`）
  - `PosConfig.Save(string path, Point p)`（写 `X=`/`Y=` 两行）
  - `PosConfig.Clamp(Point p, Rectangle area) -> Point`（越界坐标贴回 area 边界）
  - `PosConfig.DefaultPath -> string`（`%APPDATA%\StockWidget\pos.ini`）
  - `WidgetForm` 行为：按住左键拖动，松手保存；启动 `TryLoad`→`Clamp` 应用位置；圆角 Region + `Opacity = 0.92`

- [ ] **Step 1: 写失败用例**

```csharp
fail += Run("PosConfig save load roundtrip", () => {
    string p = Path.Combine(Path.GetTempPath(), "pos_test.ini");
    PosConfig.Save(p, new Point(123, 456));
    Point r = PosConfig.Load(p);
    Assert(r.X == 123 && r.Y == 456);
    Assert(PosConfig.Load(Path.Combine(Path.GetTempPath(), "no_such_pos.ini")) == Point.Empty);
    File.Delete(p);
});
fail += Run("PosConfig.Clamp", () => {
    var area = new Rectangle(0, 0, 1920, 1040);
    Assert(PosConfig.Clamp(new Point(-50, 10), area) == new Point(0, 10));
    Assert(PosConfig.Clamp(new Point(10, -5), area) == new Point(10, 0));
    Assert(PosConfig.Clamp(new Point(5000, 5000), area) == new Point(1920 - 1, 1040 - 1));
    Assert(PosConfig.Clamp(new Point(100, 100), area) == new Point(100, 100));
});
```

（`SelfTest.cs` 顶部补 `using System.Drawing;`、`using System.IO;`；`build.ps1` 的 SelfTest 清单已含 `PosConfig.cs` 与 `-r:System.Drawing.dll`，无需改动）

- [ ] **Step 2: 运行验证失败**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 编译错误 CS0246（`PosConfig` 不存在），退出码非 0

- [ ] **Step 3: 实现 `src/PosConfig.cs`**

`public static class PosConfig`。路径常量：`Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StockWidget", "pos.ini")`。`Load(path)`：文件不存在/解析失败（非整数）→ `Point.Empty`；`Save`：`Directory.CreateDirectory` 父目录后写两行；`Clamp`：`X = Math.Max(area.Left, Math.Min(p.X, area.Right - 1))`，Y 同理。`TryLoad` 糖：`var p = Load(DefaultPath); return p != Point.Empty;`（若需要可并入 Form，非必须）。

- [ ] **Step 4: `WidgetForm` 接入拖动/持久化/样式**

- 启动：`Point p = PosConfig.Load(PosConfig.DefaultPath); if (p == Point.Empty) p = 默认右上角; Location = PosConfig.Clamp(p, Screen.WorkingArea.Bounds);`
- 拖动：`MouseDown`（左键）记录偏移并 `Capture = true`；`MouseMove`（按住中）`Location = PointToScreen 差值法` 更新；`MouseUp` → `PosConfig.Save(PosConfig.DefaultPath, Location)`
- 样式：`Opacity = 0.92`；`Resize` 时用 `GraphicsPath`（`AddArc` 四角，半径 10）重建 `Region`

- [ ] **Step 5: 运行验证（单测 + 手动）**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1`
Expected: 新增 2 个 `PosConfig` 用例 `PASS`，`ALL PASS`、`BUILD OK`

手动核对：拖到左下角 → 任务管理器结束进程 → 重新运行 → 出现在左下角；改 `pos.ini` 为 `X=-9999` → 启动后仍贴在屏幕内；视觉上圆角 + 半透明生效

- [ ] **Step 6: Commit**

```bash
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "feat: draggable position persistence with rounded translucent style"
```

---

### Task 9: 单实例 + 开机自启 + README + 终验

**Files:**
- Create: `src/Autostart.cs`
- Modify: `src/Program.cs`（Mutex 单实例、调用 `Autostart.Ensure`）
- Create: `README.md`
- Create: `stocks.txt.sample`

**Interfaces:**
- Consumes: Task 6/7 的 `WidgetForm(bool)` 与 `--fetch`/`--no-desktop-layer` 分支
- Produces:
  - `Autostart.Ensure() -> void`（幂等写 HKCU Run 键 `StockWidget`）
  - `Program.Main` 顺序：Mutex 检查 → `Autostart.Ensure()` → `--fetch` 分支 → 窗体路径

- [ ] **Step 1: 实现 `src/Autostart.cs`**

`public static class Autostart`。`Ensure()`：`Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)`，`SetValue("StockWidget", "\"" + Application.ExecutablePath + "\"")`——注意避免 WinForms 依赖：用 `Assembly.GetEntryAssembly().Location` 取路径（`using System.Reflection;`），值格式 `"<全路径>"`。

- [ ] **Step 2: `Program.cs` 加单实例 + 自启**

`Main` 首行：`bool created; using (var mutex = new Mutex(true, @"Local\StockWidget_SingleInstance", out created)) { if (!created) { MessageBox.Show("股票挂件已在运行", "StockWidget"); return 0; } Autostart.Ensure(); ...其余分支不变; GC.KeepAlive(mutex); }`（`using` 包住整个 Main 生命周期，防止 Mutex 被提前回收）

- [ ] **Step 3: 写 `README.md` 与 `stocks.txt.sample`**

`stocks.txt.sample` 内容：
```
# 一行一个 6 位股票代码，# 后为注释
600519
000001
```
`README.md`（中文，简短）：用途一句话；使用 = 把 sample 复制到桌面改名为 `stocks.txt` 并编辑；运行 = 双击 `bin\StockWidget.exe`（首次运行自动写开机自启）；卸载 = 删 `bin\` 目录 + 删注册表键 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `StockWidget` + 删 `%APPDATA%\StockWidget`；已知取舍 = 免费行情秒级延迟、无法定假日日历。

- [ ] **Step 4: 构建 + 终验（spec 验证清单全过）**

Run: `powershell -ExecutionPolicy Bypass -File E:\stock-widget\build.ps1` → Expected: `BUILD OK`，单测全过

Run: `reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v StockWidget` → Expected: 含 exe 路径

手动清单（运行 exe 后逐项核对）：
- [ ] `Win+D` 桌面可见；任务栏/Alt+Tab 无入口；不压其他窗口
- [ ] 改 `stocks.txt` 生效；删文件出提示；`--fetch` 正常
- [ ] 盘中/午休/已收盘/周末状态与时间一致；断网保留旧数据 + `网络异常`
- [ ] 拖动后重启位置保留；`pos.ini` 越界被贴回屏内
- [ ] 双击第二次 exe → 弹「股票挂件已在运行」
- [ ] 任务管理器内存 < 100MB
- [ ] `--no-desktop-layer` 降级路径可用

- [ ] **Step 5: Commit**

```bash
git -C E:\stock-widget add -A
git -C E:\stock-widget commit -m "feat: single instance, autostart, readme and final verification"
```
