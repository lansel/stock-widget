# 股票搜索加入列表 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在搜索窗把选中结果加入当前 stocks.txt，自动去重、数量不限，挂件热更新。

**Architecture:** 新增纯文件追加器 `StockListWriter`（可自测）；`SearchForm` 增加底部按钮和已加入反馈；`WidgetForm` 只增加一个回调把当前 `_configPath` 交给写入器。

**Tech Stack:** C# 5 / .NET Framework 4.x / WinForms / 系统 csc，零第三方依赖。

**Spec:** `docs/superpowers/specs/2026-11-16-stock-search-design.md`（含 2026-11-17 补充）

## Global Constraints

- C# 5：不使用字符串插值、`?.`、expression-bodied 成员。
- 构建与自测唯一入口：`powershell -ExecutionPolicy Bypass -File build.ps1`，必须 `ALL PASS` + `BUILD OK`。
- 不新增第三方依赖；不设置数量上限；不写别名。
- **不执行 git commit / git add**；工作区已有的未提交改动（MA30 等）不得回退。
- 追加内容必须是纯 ASCII，避免影响原文件编码；一次 `File.AppendAllText` 写完。
- 搜索窗现有行为（搜索、防抖、打开图表、主题、错误提示）不变。

## Review Focus

- 已有 `600519` / `sh600519` / `1A0001` 时重复加入同一标的必须识别（去重归一化）。
- 上证指数 `sh000001` 加入时不能写成 `000001`（会变成平安银行）；`000300` 等能还原的指数仍写裸代码。
- 文件原内容（注释、别名、顺序）保持不变；文件末尾无换行时不能把新代码接在上一行后面。
- 文件不存在时创建；写入失败不能影响挂件运行。
- 加入后热更新多一行、搜索窗保持打开且行尾显示「已加入」；再次加入不重复写。

---

### Task 5: StockListWriter

**Files:**
- Create: `src/StockListWriter.cs`
- Modify: `selftest/SelfTest.cs`
- Modify: `build.ps1`（自测源文件列表）

**Interfaces:**
- Produces:
  - `public enum StockListAddResult { Added, AlreadyExists, Failed }`
  - `public static class StockListWriter { public static string ToConfigCode(StockSearchResult result); public static StockListAddResult Add(string path, StockSearchResult result); }`
- `ToConfigCode`：`StockList.ToSymbol(result.Code) == result.Symbol` 时返回裸 `Code`，否则返回 `Symbol`（市场前缀+代码）。
- `Add`：读取现有文件（不存在视为空）→ `StockList.ParseWidgetSettings` 解析 → 用 `StockList.ToSymbol` 归一化比较去重（同时比较 `result.Symbol`）→ 追加 `ToConfigCode(result)` + 换行；原文件末尾无 `\n` 时先补 `Environment.NewLine`；任何异常返回 `Failed`。

- [ ] **Step 1: 写失败测试**

在 `SelfTest.cs` 的 `StockSearchClient.BuildUrl` 测试之后插入：

```csharp
fail += Run("StockListWriter add to new file", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        var r = new StockSearchResult { Market = "sh", Code = "600519", Name = "X", Type = "GP-A" };
        Assert(StockListWriter.Add(p, r) == StockListAddResult.Added);
        Assert(File.ReadAllText(p) == "600519" + Environment.NewLine);
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter append preserves content", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        File.WriteAllText(p, "# c\n600519 茅台\n000001\n");
        var r = new StockSearchResult { Market = "sz", Code = "300750", Name = "X", Type = "GP-A" };
        Assert(StockListWriter.Add(p, r) == StockListAddResult.Added);
        var codes = StockList.ParseFileContent(File.ReadAllText(p));
        Assert(codes.Count == 3 && codes[2] == "300750");
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter file without trailing newline", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        File.WriteAllText(p, "600519");
        var r = new StockSearchResult { Market = "sz", Code = "000001", Name = "X", Type = "GP-A" };
        Assert(StockListWriter.Add(p, r) == StockListAddResult.Added);
        Assert(File.ReadAllText(p) == "600519" + Environment.NewLine + "000001" + Environment.NewLine);
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter duplicate detection", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        File.WriteAllText(p, "600519 茅台\nsh000001\n");
        var stock = new StockSearchResult { Market = "sh", Code = "600519", Name = "X", Type = "GP-A" };
        var index = new StockSearchResult { Market = "sh", Code = "000001", Name = "X", Type = "ZS" };
        string before = File.ReadAllText(p);
        Assert(StockListWriter.Add(p, stock) == StockListAddResult.AlreadyExists);
        Assert(StockListWriter.Add(p, index) == StockListAddResult.AlreadyExists);
        Assert(File.ReadAllText(p) == before);
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter ambiguous index code", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        var index = new StockSearchResult { Market = "sh", Code = "000001", Name = "X", Type = "ZS" };
        var hs300 = new StockSearchResult { Market = "sh", Code = "000300", Name = "X", Type = "ZS" };
        var stock = new StockSearchResult { Market = "sz", Code = "000001", Name = "X", Type = "GP-A" };
        Assert(StockListWriter.Add(p, index) == StockListAddResult.Added);
        Assert(StockListWriter.Add(p, hs300) == StockListAddResult.Added);
        Assert(StockListWriter.Add(p, stock) == StockListAddResult.Added);
        var lines = File.ReadAllText(p).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert(lines.Length == 3);
        Assert(lines[0] == "sh000001" && lines[1] == "000300" && lines[2] == "000001");
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter failure path", () => {
    var r = new StockSearchResult { Market = "sh", Code = "600519", Name = "X", Type = "GP-A" };
    Assert(StockListWriter.Add(Path.GetTempPath(), r) == StockListAddResult.Failed);
});
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: csc 编译错误，找不到 `StockListWriter` / `StockListAddResult`。

- [ ] **Step 3: 实现 `src/StockListWriter.cs`**

按 Interfaces 实现。去重集合：对 `settings.Codes` 每个值调用 `StockList.ToSymbol`；命中 `known.Contains(StockList.ToSymbol(result.Code)) || known.Contains(result.Symbol)` 即 `AlreadyExists`。追加用 `File.AppendAllText(path, prefix + code + Environment.NewLine, new UTF8Encoding(false))`。

- [ ] **Step 4: 加入自测源文件列表**

`build.ps1` 文件数组末尾加入 `"StockListWriter.cs"`。

- [ ] **Step 5: 运行构建与自测**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 新增 6 项 PASS，`ALL PASS` + `BUILD OK`（共 92 项）。

- [ ] **Step 6: 检查点**

报告结果。不要提交。

---

### Task 6: 搜索窗按钮与集成

**Files:**
- Modify: `src/SearchForm.cs`
- Modify: `src/WidgetForm.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `StockListWriter`、`StockListAddResult`（Task 5）。
- Produces:
  - `SearchForm(UiMetrics metrics, Rectangle widgetBounds, Action<StockSearchResult> onOpen, Func<StockSearchResult, StockListAddResult> onAdd)`
  - `WidgetForm` 私有方法 `StockListAddResult AddToListToConfig(StockSearchResult result)`

**SearchForm 改动：**

- 客户区高度改为 `Round(300 * scale)`；列表下边界 `ClientSize.Height - Round(48 * scale)`，列表高度相应缩短。
- 底部：左侧状态 `Label`（省略、灰色），右侧两个可点击 `Label`：「加入列表」「打开图表」（`Cursors.Hand`，灰色，宽约 `72 * scale`，高 `28 * scale`，底部居中）。
- 状态文案与颜色：成功「已加入列表」绿色（`Color.FromArgb(50,190,120)`）；重复「已在列表中」灰色；失败「加入失败」橙色（`Color.FromArgb(255,170,60)`）；无选中「先选择一只股票」灰色。
- 新增 `HashSet<string> _addedSymbols`；加入成功或已存在时加入 `result.Symbol`；自绘行时类型列显示「已加入」绿色，否则显示 `TypeLabel`。
- `AddSelected()`：取 `_list.SelectedItem`；空则提示；回调返回 `Added` → 状态「已加入列表」并标记；`AlreadyExists` →「已在列表中」并标记；`Failed` →「加入失败」。完成后 `_list.Invalidate()`。
- 「加入列表」点击 → `AddSelected()`；「打开图表」点击 → `OpenSelected()`；`Ctrl+Enter` → `AddSelected()`；`Enter`/双击仍打开图表。
- 新查询/清空结果时清空状态文字；主题热更新时同步底部文字颜色（按钮保持灰色）。

**WidgetForm 改动：**

- `new SearchForm(_metrics, Bounds, OpenChartFromSearch, AddToListToConfig)`
- `private StockListAddResult AddToListToConfig(StockSearchResult result) { return StockListWriter.Add(_configPath, result); }`
- 其余菜单、主题同步、单例逻辑不变。

**README：**

- 「多标的」条目把「（建议 1–5 只）」改成「（数量不限）」。
- 搜索功能条目改为：搜索后可打开图表或加入列表；加入写入当前 stocks.txt，自动去重。
- 操作条目补充：选中结果后可点「加入列表」，或点「打开图表」/双击/Enter 打开；`Ctrl+Enter` 也可加入。
- 已知取舍补充：加入列表只写代码不写别名；同一标的不重复写；列表数量不限。
- 自测数量改为 92。

- [ ] **Step 1: 修改 `src/SearchForm.cs`**

按上述规格实现。按钮用 `Label` 保持现有深色风格；不要引入新控件库。

- [ ] **Step 2: 修改 `src/WidgetForm.cs`**

更新 `SearchForm` 构造调用并新增回调方法。

- [ ] **Step 3: 构建**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `ALL PASS` + `BUILD OK`。

- [ ] **Step 4: 手动/GUI 验证**

- 搜索 600519 → 点「加入列表」→ 搜索窗保持打开、行尾「已加入」、stocks.txt 末尾出现 `600519`、挂件自动多一行。
- 再次加入同一只 →「已在列表中」，文件不变。
- 搜索 000001 → 选「上证指数」加入 → 文件写入 `sh000001`；再选「平安银行」加入 → 写入 `000001`；两只都在挂件出现。
- 文件末尾无换行、含注释/别名时加入 → 原内容不变、新代码单独一行。
- 无 stocks.txt 时加入 → 创建文件并生效。
- 打开图表按钮、Enter、双击、Esc 行为不变；主题热更新后底部按钮/状态颜色正常。
- 连续加入多只（≥6 只）→ 挂件行数持续增加，无数量上限。

- [ ] **Step 5: 更新 README**

按上述文案修改，核对自测数量为 92。

- [ ] **Step 6: 最终构建与检查点**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `ALL PASS` + `BUILD OK`。报告结果，不要提交。
