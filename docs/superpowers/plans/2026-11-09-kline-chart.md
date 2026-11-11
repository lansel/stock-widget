# K 线窗口 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** 双击挂件中的股票名称，打开独立窗口查看当日分时（价格线 + 成交均价线）和日 K（蜡烛 + MA5/10/20）。

**Architecture:** 新增纯逻辑层（`ChartParser` 解析腾讯接口、`ChartScale` 刻度/均线/映射）、取数层（`ChartClient`）、自绘层（`ChartCanvas` GDI+ 双缓冲）和窗口壳（`ChartForm`），由 `RowView` 的双击回调交给 `WidgetForm` 打开/复用窗口。

**Tech Stack:** C# 5 / .NET Framework 4.x、WinForms、GDI+、系统自带 csc、现有 SelfTest。

**Spec:** `docs/superpowers/specs/2026-11-09-kline-chart-design.md`

## Global Constraints

- C# 5：不用字符串插值、`?.`、expression-bodied
- 零第三方依赖；新增代码不得引入新的程序集引用（System.Windows.Forms / System.Drawing / System.Net.Http / System.Core 已有）
- 不带 `?` 的 C# 版本：nullable 不存在，注意空引用判断
- 构建与自测唯一入口：`powershell -ExecutionPolicy Bypass -File build.ps1`
- 窗口不进任务栏/Alt+Tab；普通非置顶；Win+D 随普通窗口隐藏
- 休市日挂件仍只显示“今日休市”，图表入口不可用
- 仓库现有代码风格：PascalCase 类/方法、私有字段 `_camelCase`、Selftest 用 `Run(name, () => …)`
- 不自动 commit，等最终确认

## Review Focus

1. 指数（sh000/sz399/bj899）没有有意义的成交均价：不能画均价线，也不能让它在 y 轴范围里把图压扁。
2. 分时接口跨午休：11:30 与 13:00 都映射到 120，不能画出穿透午休的假线。
3. 日 K 接口 key 因标的类型不同（`qfqday` vs `day`），缺失时必须回退，且过滤空字符串/坏行。
4. 双击名称不能被现有拖动 Capture 吃掉；双击后挂件的拖动、位置保存行为不能坏。
5. 异步取数返回时用户已切换标的/模式/关窗：旧响应必须丢弃，不能覆盖新数据或抛 ObjectDisposedException。

## File Structure

- Create `src/ChartParser.cs`：数据模型 + 腾讯 JSON 轻量解析 + 时间映射 + 均价计算；纯逻辑，无 UI。
- Create `src/ChartScale.cs`：nice ticks、均线、可见窗口、百分比/日期格式化、涨停判定；纯逻辑。
- Create `src/ChartClient.cs`：两个 URL 构造 + UTF-8 取数 + 5 秒超时。
- Create `src/ChartCanvas.cs`：GDI+ 双缓冲绘制（分时/日K/十字光标/空态/错误态/涨停柱）。
- Create `src/ChartForm.cs`：无边框窗口、标题栏、分时/日K 切换、拖动、位置记忆、刷新计时、异步取数。
- Modify `src/RowView.cs`：名称标签 DoubleClick 回调 + tooltip 提示。
- Modify `src/WidgetForm.cs`：持有/复用 ChartForm、主题热更新。
- Modify `selftest/SelfTest.cs`：新增解析/刻度/均线/映射/涨停用例。
- Modify `build.ps1`：自测源文件加入 `ChartParser.cs`、`ChartScale.cs`。
- Modify `README.md`、`src/AssemblyInfo.cs`、`docs/2026-10-30-desktop-stock-widget-design.md`。

---

### Task 1: 解析与刻度纯逻辑 + 自测

**Files:**
- Create: `src/ChartParser.cs`
- Create: `src/ChartScale.cs`
- Modify: `selftest/SelfTest.cs`
- Modify: `build.ps1`

**Interfaces:**
- Produces:
  - `class MinutePoint { string Time; int MinuteIndex; double Price; double CumVolume; double CumAmount; double Avg; }`
  - `class ChartQuote { string Name; double Price; double PrevClose; double Pct; }`
  - `class MinuteSeries { string Symbol; string Date; bool HasAvg; ChartQuote Quote; List<MinutePoint> Points; }`
  - `class DailyBar { string Date; double Open; double Close; double High; double Low; double Volume; }`
  - `class DailySeries { string Symbol; ChartQuote Quote; List<DailyBar> Bars; }`
  - `static class ChartParser { static int TimeToMinuteIndex(string hhmm); static MinuteSeries ParseMinute(string symbol, string json); static DailySeries ParseDaily(string symbol, string json); static bool IsIndexSymbol(string symbol); }`
  - `static class ChartScale { static List<double> NiceTicks(double min, double max, int targetCount); static double[] MovingAverage(IList<DailyBar> bars, int period); static int VisibleStart(int total, int visible); static double PercentFromPrev(double price, double prevClose); static string FormatShortDate(string isoDate); static double LimitRatio(string symbol, string name); static bool IsLimitUp(DailyBar bar, DailyBar prev, double limitRatio); }`

- [x] **Step 1: 在 `selftest/SelfTest.cs` 写失败用例**

用例覆盖：时间映射边界（0930→0、1130→120、1300→120、1500→240、0929/1131/1501→-1）；分时样例（3 点、date、prevClose、quote、首点均价≈1440.06）；指数分时 `HasAvg=false`；零成交量均价回退价格；日 K 的 `qfqday` 与指数 `day` 回退；坏行跳过；`NiceTicks(1400,1450,5)` 步长 10 且落在范围内；`NiceTicks(x,x,5)` 不无限循环；`MovingAverage([1..6],5)` 前 4 个 NaN、第 5 个 3、第 6 个 4；`VisibleStart(120,55)==65`、`VisibleStart(10,55)==0`；`PercentFromPrev(11,10)==10`、`(9,10)==-10`、prev=0 返回 0；`FormatShortDate("2026-11-09")=="11-09"`。

- [x] **Step 2: 跑构建确认失败**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 编译错误（ChartParser/ChartScale 或方法不存在）

- [x] **Step 3: 实现 `ChartParser.cs`**

轻量扫描器（字符串数组、数组的数组），不做通用 JSON。分时定位 `<symbol>` 节点后的 `"data":[`；日 K 先找 `"qfqday":[`，为空再找 `"day":[`。数值用 InvariantCulture；坏行跳过。`HasAvg = !IsIndexSymbol(symbol)`；均价 = 累计额/(累计量×100)，量为 0 用价格。指数判定：`sh000`、`sz399`、`bj899` 前缀。

- [x] **Step 4: 实现 `ChartScale.cs`**

`NiceTicks` 用 1/2/5×10ⁿ 步长；min==max 时按 ±1% 扩开。`MovingAverage` 不足周期返回 `double.NaN`。`VisibleStart` 取 `max(0, total-visible)`。`PercentFromPrev` 在昨收 ≤0 时返回 0。涨停判定：主板 10%、主板 ST 5%、创业板/科创板 20%、北交所 30%，指数/ETF 返回 0；要求收盘=最高且涨跌幅在限制附近。

- [x] **Step 5: 把两个新文件加进 `build.ps1` 自测列表并复跑**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 新用例全部 PASS，主程序构建 OK

### Task 2: 取数层

**Files:**
- Create: `src/ChartClient.cs`

**Interfaces:**
- Consumes: 无
- Produces: `static class ChartClient { static string BuildMinuteUrl(string symbol); static string BuildDailyUrl(string symbol, int bars); static string FetchMinute(string symbol); static string FetchDaily(string symbol, int bars); }`

- [x] **Step 1: 实现两个 URL 与 UTF-8 GET**

分时 `https://ifzq.gtimg.cn/appstock/app/minute/query?code=<symbol>`；日 K `https://ifzq.gtimg.cn/appstock/app/fqkline/get?param=<symbol>,day,,,<bars>,qfq`。静态 `HttpClient`，超时 5 秒，`Encoding.UTF8` 解码；静态构造里补 TLS 1.2。

- [x] **Step 2: 构建通过**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: BUILD OK

### Task 3: 自绘层

**Files:**
- Create: `src/ChartCanvas.cs`

**Interfaces:**
- Consumes: `MinuteSeries`、`DailySeries`、`ChartScale`
- Produces:
  - `sealed class ChartCanvas : Control`
  - `ChartMode Mode { get; set; }`（`Minute` / `Daily`）
  - `void SetMinute(MinuteSeries series)`
  - `void SetDaily(DailySeries series)`
  - `void SetState(bool loading, string error)`
  - `void ApplyTheme(Color background, Color text)`
  - `float UiScale { get; set; }`

- [x] **Step 1: 双缓冲与布局**

`SetStyle(AllPaintingInWmPaint | UserPaint | OptimizedDoubleBuffer, true)`；绘图区留左 56×scale、右（分时 52×scale，日K 14×scale）、上 10×scale、下 22×scale。

- [x] **Step 2: 分时绘制**

x = `MinuteIndex/240`；y 范围含昨收、价格、均价（仅 HasAvg），上下留 4%；价格网格 + 左价格标签 + 右百分比标签；昨收虚线；价格线用主题文字色，均价线琥珀色；横轴 09:30/10:30/11:30/14:00/15:00；无数据/加载/错误居中提示。

- [x] **Step 3: 日 K 绘制**

可见根数 = `max(10, plotWidth / (9×scale))`，取最后 N 根；y 范围含可见高低点和均线；红涨绿跌实体 + 影线；涨停柱改高亮样式（金色实心柱 + 亮金描边 + 顶部白色三角）；MA5 白 / MA10 黄 / MA20 紫，图例左上；横轴约 5 个 MM-DD 日期。

- [x] **Step 4: 十字光标**

鼠标移动时吸附最近分钟点/蜡烛，画竖线/横线 + 读数框：分时时间/价格/均价/涨跌幅；日K 日期/开高低收/涨跌幅/MA，涨停附“涨停”。读数框避让绘图区边界；MouseLeave 清除。

- [x] **Step 5: 构建通过**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: BUILD OK

### Task 4: 窗口壳

**Files:**
- Create: `src/ChartForm.cs`
- Modify: `src/PosConfig.cs`（新增 `ChartPath`）

**Interfaces:**
- Consumes: `ChartCanvas`、`ChartClient`、`ChartParser`、`TradingClock`、`IdleClock`、`PosConfig`
- Produces:
  - `sealed class ChartForm : Form`
  - `ChartForm(UiMetrics metrics, Rectangle widgetBounds)`
  - `void SetTarget(string code, string alias, string symbol)`
  - `void ApplyTheme(Color background, Color nameColor)`

- [x] **Step 1: 窗口与标题栏**

无边框、`ShowInTaskbar=false`、普通非置顶、固定 680×420×scale、圆角 Region；标题栏含名称/代码、现价涨跌、日期、分时/日K 切换、×；标题区域可拖动；Esc 关闭；`WS_EX_TOOLWINDOW`。

- [x] **Step 2: 位置记忆**

新增 `%APPDATA%\StockWidget\chart.ini`（复用 `PosConfig`）；无记录时贴挂件左侧，不够则右侧/下方；`Clamp` 回屏内；关闭时保存。

- [x] **Step 3: 刷新与取数**

打开/切标的/切模式立即取数；计时器：分时 15 秒、日 K 60 秒，交易日盘外 60 秒，非交易日不请求；空闲 ≥10 分钟跳过；`_busy` + 请求序号丢弃过期响应；失败保留旧数据并显示“加载失败，自动重试”。

- [x] **Step 4: 构建通过**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: BUILD OK

### Task 5: 接线

**Files:**
- Modify: `src/RowView.cs`
- Modify: `src/WidgetForm.cs`

**Interfaces:**
- Consumes: `ChartForm`
- Produces: `RowView.NameDoubleClicked`（`Action<string, string>`）

- [x] **Step 1: RowView 暴露双击**

名称标签 `DoubleClick` 触发 `NameDoubleClicked(code, alias)`；tooltip 追加“双击查看图表”；价格/涨跌幅标签不挂。

- [x] **Step 2: WidgetForm 打开/复用**

字段 `ChartForm _chart`；`OpenChart(code, alias)`：无窗口则创建并 `SetTarget`，有则切标的并 `Activate()`；窗口关闭后置空；配置快照时同步主题；双击不破坏拖动。

- [x] **Step 3: 构建通过**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: BUILD OK

### Task 6: 文档、版本与验证

**Files:**
- Modify: `README.md`
- Modify: `src/AssemblyInfo.cs`
- Modify: `docs/2026-10-30-desktop-stock-widget-design.md`

- [x] **Step 1: 更新 README**

功能、操作（双击名称 / 切换 / Esc / 位置记忆）、已知取舍（休市日无入口、无缩放/更多周期/技术指标、免费接口延迟）；自测数量改为实际值。

- [x] **Step 2: 版本与主设计文档**

`AssemblyInfo` → 1.5.0.0；主设计文档加“增补 8（v1.5.0）”。

- [x] **Step 3: 全量验证**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 全部自测 PASS、BUILD OK

- [x] **Step 4: 手工冒烟**

运行 `bin\StockWidget.exe`，双击 600519 / 1A0001 / 159915 名称；验证分时均价、日K、十字光标、切换、Esc、位置记忆、断网提示、125% DPI；确认拖动与休市日行为不变。

## 执行结果

- `build.ps1`：自测全部 PASS，BUILD OK（2026-11-09 为 70 项，后续调整后为 78 项）
- 真实数据渲染：600519 / sh000001 / sz159915 的分时与日 K 均正确；指数分时不画均价线
- 涨停柱：sz000977、sh603259、sz300223 真实数据渲染通过；ST / 指数 / ETF / 普通大阳线用例由 SelfTest 覆盖
- 窗口集成：双击名称打开、再次双击复用同一窗口并切换标的、关闭后字段清空、重新打开新窗口
- 模拟 125% 缩放布局无溢出；真实 DPI 字体缩放依赖系统 DPI
- 已知未在本机验证：真实 125%/150% 系统 DPI 下的最终观感（本机 DPI=100%）
- 因本机已有旧版 StockWidget 实例在运行（单实例互斥），未启动新 exe 做替换测试；改用同一套 WidgetForm/ChartForm 直接实例化完成等价冒烟

## 后续调整（2026-11-09）

- 日 K 涨停柱高亮标识（方案 A 继续调整）：新增 `ChartScale.LimitRatio` / `IsLimitUp` + 2 项自测；`ChartCanvas` 将涨停柱改为金色实心柱 + 亮金描边 + 顶部白色三角，十字光标读数附“涨停”
- 数据源主机从 `web.ifzq.gtimg.cn` 切换为 `ifzq.gtimg.cn`（日 K 接口稳定可用）

## 后续调整（2026-11-10）

- 日 K 增加量能副图：主图下方约 26% 高度；成交量柱与蜡烛同色（涨停日金色），左轴按万/亿缩写（单位手）；十字光标竖线贯穿两窗格，量能区横线吸附柱顶，读数新增“量 ... 手”
- 分时也增加量能副图：按累计成交量差分得到每分钟量能，11:30/13:00 合并到同一横轴刻度；柱色按分钟价格涨跌，十字光标读数新增“量 ... 手”
- 增加周 K/月 K：标题栏四按钮；`ParseKline` 支持 `qfqweek/qfqmonth` 与指数 `week/month`；周/月 K 不自动刷新，进入模式时取一次；涨停金色仅日 K；日期轴周 `yy-MM-DD`、月 `yyyy-MM`
- 分时叠加 3/5/10/20 日动态均线：含今日现价，左上角显示数值；范围内画横向虚线，超出加 `↑`/`↓` 不画线；进入分时时按需额外拉一次日 K 作参考
- `ChartScale` 新增 `MaxVolume` / `FormatVolume` / `MinuteVolumes` / `IntradayMa` / `FormatWeek` / `FormatMonth`，`ChartParser` 新增 `ParseKline`，`ChartClient` 新增 `BuildKlineUrl`；自测总数 78
- 真实数据渲染：600519 / sh000001 / sz159915 的日/周/月 K 与分时均线正常；sz000977、sh603259 周/月 K 无金色涨停柱；sz000977 / sh000001 / sz159915 分时均线范围内/外表现正确；125% 缩放下四按钮与均线图例正常
