# 股票搜索 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 右键菜单打开股票搜索窗口，选中搜索结果后复用现有 K 线窗口查看，不写 stocks.txt。

**Architecture:** 新增纯解析器（可自测）、腾讯联想接口客户端和独立搜索 Form；`WidgetForm` 只增加菜单入口、主题同步和带 symbol 的 `OpenChart` 重载。现有行情轮询、拖动、桌面层行为与 `ChartForm` 不改。

**Tech Stack:** C# 5 / .NET Framework 4.x / WinForms / 系统 csc，零第三方依赖。

**Spec:** `docs/superpowers/specs/2026-11-16-stock-search-design.md`

## Global Constraints

- C# 5：不使用字符串插值、`?.`、expression-bodied 成员；UI 用 WinForms，Win32 用现有 P/Invoke 风格。
- 构建与自测唯一入口：`powershell -ExecutionPolicy Bypass -File build.ps1`，必须看到 `ALL PASS` 与 `BUILD OK`。
- 主程序自动编译 `src\*.cs`；自测源文件列表在 `build.ps1` 里显式维护，新增纯逻辑文件要手动加入。
- 不新增第三方依赖；不写 stocks.txt；不改 `ChartForm.cs`、`QuoteClient.cs`、轮询/活动感知代码。
- **不要执行 git commit / git add**（用户全局规则：未明确要求不提交）。工作区已有与本功能无关的未提交改动（`README.md`、`selftest/SelfTest.cs`、`src/ChartCanvas.cs`、`docs/superpowers/...` 的 MA30 内容），不要回退、不要整文件暂存。
- 后台请求沿用现有模式：`Task.Factory.StartNew` + `BeginInvoke`，回调前检查 `IsDisposed`/`_closed`。
- 所有中文文案、接口 URL、类型白名单与本文一致。

## Review Focus

- 查询含空格、`&`、`%`、中文时 URL 编码正确（`BuildUrl` 自测覆盖）。
- 响应为 `v_hint="N"`、空串、乱码或字段不足时不崩溃、返回空列表（解析器自测覆盖）。
- 同码不同标的（`000001` 指数 vs 平安银行）必须按接口给的市场前缀打开，不能靠 `ToSymbol` 猜测（解析器自测 + 手动验证）。
- 快速连续输入时旧请求的迟到响应不得覆盖新查询（请求序号；手动验证）。
- 断网/超时显示「搜索失败，请重试」，不阻塞挂件；中文输入法下回车不误打开结果；关闭后重开单实例正常（手动验证）。
- 搜索窗关闭后再次打开不能抛出 `ObjectDisposedException`；挂件退出时搜索窗一并关闭（手动验证）。

---

### Task 1: 搜索解析器

**Files:**
- Create: `src/StockSearchParser.cs`
- Modify: `selftest/SelfTest.cs`（在 `QuoteParser` 测试块之后插入）
- Modify: `build.ps1:5-8`（自测源文件列表）

**Interfaces:**
- Produces:
  - `public sealed class StockSearchResult { public string Market; public string Code; public string Name; public string Type; public string Symbol { get; } }`，`Symbol` 返回 `Market + Code`。
  - `public static class StockSearchParser { public const int MaxResults = 10; public static List<StockSearchResult> Parse(string responseText); public static string TypeLabel(string type); }`
- `Parse` 规则：用 `v_hint\s*=\s*"([^"]*)"` 取载荷；载荷为 `N`、空或无匹配时返回空列表；按 `^` 分条目、`~` 分字段（市场~代码~名称~拼音~类型，至少 5 段）；名称用 `\uXXXX` 反转义；市场规范为小写且只保留 `sh`/`sz`；代码必须是 6 位数字；类型白名单 `GP-A`、`GP-B`、`ZS`、`ETF`、`LOF`（忽略大小写）；跳过空名称；保持接口顺序，最多 `MaxResults` 条。
- `TypeLabel`：`GP-A`→`股票`、`GP-B`→`B股`、`ZS`→`指数`、`ETF`→`ETF`、`LOF`→`LOF`，未知类型原样返回。

- [ ] **Step 1: 写失败测试**

在 `selftest/SelfTest.cs` 的 `QuoteParser` 测试块之后插入（`using System.Text;` 已存在）：

```csharp
fail += Run("StockSearchParser.Parse single result", () => {
    var r = StockSearchParser.Parse(
        "v_hint=\"sh~600519~\\u8d35\\u5dde\\u8305\\u53f0~gzmt~GP-A\";");
    Assert(r.Count == 1);
    Assert(r[0].Market == "sh" && r[0].Code == "600519");
    Assert(r[0].Name == "\u8d35\u5dde\u8305\u53f0");
    Assert(r[0].Type == "GP-A" && r[0].Symbol == "sh600519");
});
fail += Run("StockSearchParser.Parse multi filter markets", () => {
    string text = "v_hint=\"sh~000001~\\u4e0a\\u8bc1\\u6307\\u6570~szzs~ZS"
        + "^sz~000001~\\u5e73\\u5b89\\u94f6\\u884c~payh~GP-A"
        + "^jj~000001~\\u534e\\u590f\\u6210\\u957f\\u6df7\\u5408~hxczhh~KJ"
        + "^hk~00700~\\u817e\\u8baf\\u63a7\\u80a1~txkg~GP"
        + "^us~tcehy.ps~TCEHY~GP\";";
    var r = StockSearchParser.Parse(text);
    Assert(r.Count == 2);
    Assert(r[0].Market == "sh" && r[0].Name == "\u4e0a\u8bc1\u6307\u6570");
    Assert(r[1].Market == "sz" && r[1].Name == "\u5e73\u5b89\u94f6\u884c");
    Assert(r[0].Symbol == "sh000001" && r[1].Symbol == "sz000001");
});
fail += Run("StockSearchParser.Parse no match and garbage", () => {
    Assert(StockSearchParser.Parse("v_hint=\"N\";").Count == 0);
    Assert(StockSearchParser.Parse("v_hint=\"\";").Count == 0);
    Assert(StockSearchParser.Parse("garbage").Count == 0);
    Assert(StockSearchParser.Parse(null).Count == 0);
    Assert(StockSearchParser.Parse("").Count == 0);
});
fail += Run("StockSearchParser.Parse whitelist and malformed", () => {
    string text = "v_hint=\"sh~110059~x~x~KZZ"
        + "^sz~123220~y~y~KZZ"
        + "^sh~900901~\\u4e91\\u8d5bB\\u80a1~ysbg~GP-B"
        + "^sz~161725~z~z~LOF"
        + "^sh~600519~\\u8d35\\u5dde\\u8305\\u53f0~gzmt~GP-A"
        + "^sz~abc~bad~x~GP-A"
        + "^sz~12345~bad~x~GP-A"
        + "^sh~600520~only\";";
    var r = StockSearchParser.Parse(text);
    Assert(r.Count == 3);
    Assert(r[0].Type == "GP-B" && r[1].Type == "LOF" && r[2].Type == "GP-A");
    Assert(r[2].Name == "\u8d35\u5dde\u8305\u53f0");
});
fail += Run("StockSearchParser.Parse caps at 10", () => {
    var sb = new StringBuilder("v_hint=\"");
    for (int i = 0; i < 12; i++) {
        if (i > 0) sb.Append('^');
        sb.Append("sh~6000" + i.ToString("00") + "~N" + i + "~x~GP-A");
    }
    sb.Append("\";");
    var r = StockSearchParser.Parse(sb.ToString());
    Assert(r.Count == 10);
    Assert(r[9].Code == "600009");
});
fail += Run("StockSearchParser.Parse normalizes market case", () => {
    var r = StockSearchParser.Parse("v_hint=\"SH~600519~X~x~gp-a\";");
    Assert(r.Count == 1 && r[0].Market == "sh" && r[0].Type == "gp-a");
    Assert(StockSearchParser.TypeLabel(r[0].Type) == "\u80a1\u7968");
});
fail += Run("StockSearchParser.TypeLabel", () => {
    Assert(StockSearchParser.TypeLabel("GP-A") == "\u80a1\u7968");
    Assert(StockSearchParser.TypeLabel("GP-B") == "B\u80a1");
    Assert(StockSearchParser.TypeLabel("ZS") == "\u6307\u6570");
    Assert(StockSearchParser.TypeLabel("ETF") == "ETF");
    Assert(StockSearchParser.TypeLabel("LOF") == "LOF");
    Assert(StockSearchParser.TypeLabel("KZZ") == "KZZ");
});
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: csc 编译错误，提示找不到类型 `StockSearchParser` / `StockSearchResult`。

- [ ] **Step 3: 实现 `src/StockSearchParser.cs`**

按 Interfaces 的签名与规则实现。算法要点：

```csharp
// 解析入口
private static readonly Regex HintRegex = new Regex("v_hint\\s*=\\s*\"([^\"]*)\"", RegexOptions.Compiled);
private static readonly Regex CodeRegex = new Regex(@"^\d{6}$", RegexOptions.Compiled);
private static readonly HashSet<string> TypeWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    { "GP-A", "GP-B", "ZS", "ETF", "LOF" };

// \uXXXX 反转义：扫描字符串，遇到 '\\' + 'u' + 4 位十六进制时转成 (char)，
// int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)
// 失败则原样保留；其余字符复制。
```

`Parse` 用 `HintRegex.Match`；`payload == "N"` 直接返回空列表；每段 `Trim()` 后按规则过滤；`Market = market.ToLowerInvariant()`；名称按原样反转义；达到 10 条立即停止。

- [ ] **Step 4: 把解析器加入自测源文件列表**

`build.ps1` 第 5 行的文件数组末尾加入 `"StockSearchParser.cs"`（放在 `"ChartClient.cs"` 之后，注意逗号）。

- [ ] **Step 5: 运行构建与自测，确认通过**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 新增 7 个测试全部 PASS，最后 `ALL PASS` 与 `BUILD OK`。

- [ ] **Step 6: 检查点**

报告新增测试数量与构建结果。不要提交。

---

### Task 2: 搜索接口客户端

**Files:**
- Create: `src/StockSearchClient.cs`
- Modify: `selftest/SelfTest.cs`（Task 1 测试块之后）
- Modify: `build.ps1`（自测源文件列表再加一项）

**Interfaces:**
- Produces:
  - `public static class StockSearchClient { public static string BuildUrl(string query); public static string FetchRaw(string query); }`
- `BuildUrl`：`"https://smartbox.gtimg.cn/s3/?v=2&q=" + Uri.EscapeDataString(query ?? "") + "&t=all"`。
- `FetchRaw`：静态 `HttpClient`，超时 3000ms，`GetByteArrayAsync(url).Result`，用 `QuoteClient.DecodeGbk(bytes)` 解码（响应是 ASCII + `\uXXXX`，GBK 解码对 ASCII 无副作用）。

- [ ] **Step 1: 写失败测试**

在 Task 1 的测试块之后插入：

```csharp
fail += Run("StockSearchClient.BuildUrl", () => {
    Assert(StockSearchClient.BuildUrl("600519")
        == "https://smartbox.gtimg.cn/s3/?v=2&q=600519&t=all");
    Assert(StockSearchClient.BuildUrl("\u8d35\u5dde\u8305\u53f0")
        == "https://smartbox.gtimg.cn/s3/?v=2&q=%E8%B4%B5%E5%B7%9E%E8%8C%85%E5%8F%B0&t=all");
    Assert(StockSearchClient.BuildUrl("a&b") == "https://smartbox.gtimg.cn/s3/?v=2&q=a%26b&t=all");
    Assert(StockSearchClient.BuildUrl("a b") == "https://smartbox.gtimg.cn/s3/?v=2&q=a%20b&t=all");
});
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: csc 编译错误，提示找不到类型 `StockSearchClient`。

- [ ] **Step 3: 实现 `src/StockSearchClient.cs`**

按 `QuoteClient.cs` 的静态客户端模式实现（`_client` + `_lock` + 双重检查），不要新增依赖。`BuildUrl` 参数为空时 `q=` 留空。

- [ ] **Step 4: 把客户端加入自测源文件列表**

`build.ps1` 文件数组末尾加入 `"StockSearchClient.cs"`。

- [ ] **Step 5: 运行构建与自测，确认通过**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 新增测试 PASS，`ALL PASS` 与 `BUILD OK`。

- [ ] **Step 6: 检查点**

报告构建结果。不要提交。

---

### Task 3: 搜索窗口与挂件集成

**Files:**
- Create: `src/SearchForm.cs`
- Modify: `src/WidgetForm.cs`（字段区、菜单构造、`ApplySnapshot`、`OpenChart`、`OnFormClosing`）

**Interfaces:**
- Consumes: `StockSearchResult`、`StockSearchParser`、`StockSearchClient`（Task 1/2）。
- Produces:
  - `public sealed class SearchForm : Form`
  - `public SearchForm(UiMetrics metrics, Rectangle widgetBounds, Action<StockSearchResult> onOpen)`
  - `public void ApplyTheme(Color background, Color nameColor)`
  - `public void FocusInput()`
- `WidgetForm` 新增私有方法：`void OpenChartWithSymbol(string code, string alias, string symbol)`、`void OpenChartFromSearch(StockSearchResult result)`、`void OnSearchClick(object sender, EventArgs e)`。

**SearchForm 行为规格：**

- 窗口：无边框、普通可激活窗口（`CreateParams` 只加 `WS_EX_TOOLWINDOW`，**不要** `WS_EX_NOACTIVATE`）；`ShowInTaskbar=false`；`TopMost=false`；`AutoScaleMode=None`；客户区 `Round(360*scale) × Round(260*scale)`；启动位置在 `widgetBounds` 所在屏幕工作区居中；`KeyPreview=true`。
- 控件与布局（`scale = metrics.Scale`）：标题「搜索股票」左上；右上 `×` 关闭；输入框 `TextBox`（`BorderStyle.FixedSingle`）从 `(12,40)` 到右侧内边距，高 30；结果列表 `ListBox`（`BorderStyle.None`、`DrawMode.OwnerDrawFixed`、`ItemHeight=Round(30*scale)`、`IntegralHeight=false`）占剩余区域；同区域一个居中灰色提示 `Label`，与列表互斥显示。
- 颜色：背景用主题色；列表底 `Shade(bg,1.08)`；输入框底 `Shade(bg,1.15)`；选中行 `Shade(bg,1.30)`；代码/类型/提示用 `Color.FromArgb(140,140,150)`。`Shade` 可复制 `ChartForm` 的私有做法（RGB 各乘系数并夹在 0–255）。
- 字体：标题 `Microsoft YaHei UI` 10.5f Bold、输入与列表 10f、关闭 `×` 12f。
- 每个结果行自绘：名称左侧、`EndEllipsis`、约 55% 宽；代码和类型右对齐、灰色；类型用 `StockSearchParser.TypeLabel`。
- 搜索流程：
  - 输入变化：`_requestId++`，重启 300ms 防抖计时器。
  - 计时器触发：取 `Text.Trim()`；空则清空列表并显示「输入代码、名称或拼音首字母」；否则 `id = ++_requestId`，后台 `StockSearchClient.FetchRaw(q)`，完成后 `BeginInvoke` 应用。
  - 应用：`_closed || id != _requestId` 时丢弃；请求异常或空响应显示「搜索失败，请重试」并清空；`StockSearchParser.Parse` 为 0 条显示「无匹配结果」；有结果则绑定到 `ListBox.Items`，默认选中第 0 条并隐藏提示。
  - 首次打开显示初始提示；关闭时 `_closed = true; _requestId++;` 停计时器。
- 键盘（在 Form 的 `OnKeyDown` 处理，吞掉按键避免提示音）：`↑`/`↓` 移动选择；`Enter` 打开选中；`Esc` 关闭。鼠标单击只选中，双击打开。
- 打开选中项：先取本地 `StockSearchResult` 与回调引用，`Close()`，再调用回调 —— 保证 K 线窗口最后获得焦点。
- `ApplyTheme`：更新背景、标题/关闭/输入框/列表/提示颜色并 `Invalidate`。
- `FocusInput()`：`Activate()` 后让输入框获得焦点（重开搜索窗时调用）。
- `Dispose(bool)`：释放计时器与字体；后台回调前检查 `IsDisposed`。

**WidgetForm 集成：**

- 字段：在 `_chart` 后加 `private SearchForm _search;`。
- 菜单：在创建 `_menu` 后先加「搜索股票…」（`Click += OnSearchClick`），再加 `new ToolStripSeparator()`，最后加「退出」。
- `ApplySnapshot`：在同步 `_chart` 主题的同一处，若 `_search != null && !_search.IsDisposed` 则 `_search.ApplyTheme(settings.BgColor, settings.NameColor)`。
- `OnSearchClick`：`_search` 为空或已释放时新建 `SearchForm(_metrics, Bounds, OpenChartFromSearch)`，保存引用、`FormClosed` 时置空、先 `ApplyTheme(BackColor, _nameColor)` 再 `Show()`；否则 `Show()`（若被隐藏）+ `Activate()` + `FocusInput()`。
- `OpenChart` 重载：原方法改为 `OpenChart(code, alias)` 调用 `OpenChartWithSymbol(code, alias, StockList.ToSymbol(code))`；原方法体整体移入 `OpenChartWithSymbol`，用传入的 `symbol`，空字符串直接返回；`OpenChartFromSearch` 调 `OpenChartWithSymbol(result.Code, result.Name, result.Symbol)`（别名传名称，标题首帧就有名字；symbol 用接口市场前缀，不走 `ToSymbol`）。
- `OnFormClosing`：在关闭 `_chart` 的同一段里，若 `_search` 存在且未释放则 `Close()`（try/catch 包裹）。

- [ ] **Step 1: 实现 `src/SearchForm.cs`**

按上述规格实现。所有尺寸用 `metrics.Scale` 缩放，`Round` 取整。

- [ ] **Step 2: 集成到 `src/WidgetForm.cs`**

按上述锚点修改。不改动行情轮询、拖动、Win+D、桌面层逻辑。

- [ ] **Step 3: 构建并跑自测**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `ALL PASS` 与 `BUILD OK`（新增 UI 文件自动被主程序编译）。

- [ ] **Step 4: 手动验证搜索链路**

Run: `Copy-Item stocks.txt.sample $env:TEMP\sw_search_test.txt; .\bin\StockWidget.exe --config $env:TEMP\sw_search_test.txt`

逐项确认：

- 右键挂件 →「搜索股票…」出现，搜索窗在挂件所在屏幕居中，输入框自动获得焦点。
- 输入 `600519` → 显示「贵州茅台 600519 股票」；`Enter` 打开 K 线窗，搜索窗关闭，图表默认分时。
- 搜索 `000001` → 同时出现「上证指数」和「平安银行」；选平安银行 → 标题为「平安银行 000001」，分时有均价线（指数无均价线）。
- 搜索 `gzmt` 命中贵州茅台；`510300` 显示 ETF；`900901` 显示 B 股。
- 快速连续输入不同关键词，最终结果只对应最后一次输入。
- 断网（或拔网线）搜索 →「搜索失败，请重试」，挂件不卡死、不崩溃。
- 用中文输入法输入「茅台」，按回车提交候选词时不会误开结果；再按回车才打开。
- 按 `Esc` 或 `×` 关闭；再次右键打开正常（无 `ObjectDisposedException`）；标记 `ShowInTaskbar` 中不可见。
- 打开图表后切换另一只搜索结果 → 复用同一个 K 线窗口并切换标的。
- 拖动挂件、双击已有股票打开图表、编辑 stocks.txt 热更新：行为不变。

- [ ] **Step 5: 检查点**

报告构建结果与手动验证情况。不要提交。

---

### Task 4: 文档与整体回归

**Files:**
- Modify: `README.md`

**Interfaces:**
- Consumes: 全部已完成功能。
- Produces: 用户可见的功能说明与已知取舍。

- [ ] **Step 1: 更新 README**

在「功能」列表的 K 线窗口条目后加：

```markdown
- 股票搜索：右键任意位置 →「搜索股票…」，按代码、名称或拼音首字母搜索，选中后直接打开 K 线窗口，不写入 stocks.txt
```

在「操作」列表中、双击看图条目后加：

```markdown
- 右键任意位置 →「搜索股票…」打开搜索窗口，↑/↓ 选择、`Enter` 或双击打开图表，`Esc` 关闭；休市日、无 stocks.txt 时也可用
```

把「已知取舍」中的休市日条目改为：

```markdown
- 休市日（周末/法定假日）挂件只显示“今日休市”、隐藏股票行，双击入口不可用；可通过搜索窗口打开 K 线查看最近交易日数据
```

在「已知取舍」中再加：

```markdown
- 股票搜索只覆盖沪深 A/B 股、指数和 ETF/LOF；港美股、北交所、可转债不在搜索结果中（北交所等仍可手写进 stocks.txt 后双击看图）
```

把「从源码构建」中的自测数量从 78 改成实际数量（按 Step 2 核对）。

- [ ] **Step 2: 核对自测数量**

Run: `(Select-String -Path selftest\SelfTest.cs -Pattern 'fail \+= Run\(').Count`
Expected: `86`（78 + Task 1 的 7 + Task 2 的 1）；若不是，以实际数字更新 README。

- [ ] **Step 3: 最终构建**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `ALL PASS` 与 `BUILD OK`。

- [ ] **Step 4: 整体回归**

- 分别在交易日与休市日（或调整系统日期到周末）启动：挂件行/「今日休市」显示正常，搜索入口都可打开。
- 无 stocks.txt（清空配置路径）时启动：挂件显示「未找到 stocks.txt」，右键搜索仍可打开图表。
- 125% / 150% DPI：搜索窗、结果行、标题、`×` 不重叠、不越界。
- 单 exe、无新增 DLL；内存与原来同一量级。
- 修改 bgColor/nameColor 后保存：已打开的搜索窗与 K 线窗颜色同步。

- [ ] **Step 5: 检查点**

报告测试数量、构建与回归结果。不要提交；等用户确认后由用户决定是否提交。
