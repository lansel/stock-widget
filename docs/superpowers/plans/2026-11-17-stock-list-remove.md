# 股票列表移出 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 右键挂件股票行 →「移出列表」，从当前 stocks.txt 删除对应行并热更新，保留注释和编码。

**Architecture:** 在 `StockListWriter` 增加字节级按行删除；`RowView` 给行标签带代码，`WidgetForm` 右键记录目标并在菜单 Opening 时决定是否显示移出项。

**Tech Stack:** C# 5 / .NET Framework 4.x / WinForms / 系统 csc，零第三方依赖。

**Spec:** `docs/superpowers/specs/2026-11-16-stock-search-design.md`（含 2026-11-17 移出列表补充）

## Global Constraints

- C# 5；构建/自测唯一入口 `powershell -ExecutionPolicy Bypass -File build.ps1`，必须 `ALL PASS` + `BUILD OK`。
- 不新增依赖；删除不弹确认、不可撤销；不改行情轮询/拖动/K 线逻辑。
- **不执行 git commit / git add**；工作区已有未提交改动不得回退。
- 删除必须保留文件其他字节（BOM、注释、别名、顺序、CRLF/LF、编码）；追加逻辑不变。

## Review Focus

- 指数与股票同码：`sh000001` 与 `000001` 只删各自的行，不能误删。
- 别名行、行内注释、空行、配置项行保留；首行删除时 UTF-8 BOM 不丢。
- 文件末尾无换行、空文件、找不到行、文件不存在都不崩溃。
- 右键空白/提示/状态点不出现「移出列表」；右键行出现且菜单高度更大。
- 移出后热更新少一行，最后一只移出后回到「请编辑 stocks.txt」。

---

### Task 7: StockListWriter.Remove

**Files:**
- Modify: `src/StockListWriter.cs`
- Modify: `src/StockList.cs`（`IsCode` 改为 public）
- Modify: `selftest/SelfTest.cs`

**Interfaces:**
- Produces: `public enum StockListRemoveResult { Removed, NotFound, Failed }`
- Produces: `public static StockListRemoveResult Remove(string path, string symbol)`
- `symbol` 为 `StockList.ToSymbol` 归一化后的市场前缀+代码；匹配时把每行按 ASCII 读取（非 ASCII 字节替换为 `?`），去掉 `#` 行内注释、跳过含 `=` 的配置行，取第一个 token，`StockList.IsCode` 校验后与 symbol 比较。
- 字节级删除：保留 UTF-8 BOM；只跳过匹配行（含其换行）；其他字节原样复制。匹配到但写入失败 → `Failed`；未匹配 → `NotFound`。

- [ ] **Step 1: 写失败测试**

在 `SelfTest.cs` 的 `StockListWriter failure path` 之后插入：

```csharp
fail += Run("StockListWriter remove line preserves content", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        File.WriteAllText(p, "# c\n600519 茅台\n000001\n");
        Assert(StockListWriter.Remove(p, "sz000001") == StockListRemoveResult.Removed);
        Assert(File.ReadAllText(p) == "# c\n600519 茅台\n");
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter remove preserves BOM", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        File.WriteAllText(p, "600519\n000001\n", new UTF8Encoding(true));
        Assert(StockListWriter.Remove(p, "sh600519") == StockListRemoveResult.Removed);
        byte[] bytes = File.ReadAllBytes(p);
        Assert(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert(File.ReadAllText(p) == "000001\n");
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter remove without trailing newline", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        File.WriteAllText(p, "600519\r\n000001");
        Assert(StockListWriter.Remove(p, "sz000001") == StockListRemoveResult.Removed);
        Assert(File.ReadAllText(p) == "600519\r\n");
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter remove not found", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        File.WriteAllText(p, "600519\n");
        string before = File.ReadAllText(p);
        Assert(StockListWriter.Remove(p, "sh000300") == StockListRemoveResult.NotFound);
        Assert(File.ReadAllText(p) == before);
    } finally { if (File.Exists(p)) File.Delete(p); }
});
fail += Run("StockListWriter remove failure path", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    if (File.Exists(p)) File.Delete(p);
    Assert(StockListWriter.Remove(p, "sh600519") == StockListRemoveResult.Failed);
});
fail += Run("StockListWriter remove index and stock by symbol", () => {
    string p = Path.Combine(Path.GetTempPath(), "sw_add_" + Guid.NewGuid().ToString("N") + ".txt");
    try {
        File.WriteAllText(p, "sh000001\n000001\n");
        Assert(StockListWriter.Remove(p, "sh000001") == StockListRemoveResult.Removed);
        Assert(File.ReadAllText(p) == "000001\n");
        Assert(StockListWriter.Remove(p, "sz000001") == StockListRemoveResult.Removed);
        Assert(File.ReadAllText(p) == "");
    } finally { if (File.Exists(p)) File.Delete(p); }
});
```

- [ ] **Step 2: 运行测试，确认失败**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: csc 编译错误，找不到 `Remove` / `StockListRemoveResult`。

- [ ] **Step 3: 实现**

`StockList.IsCode` 改为 `public static`。`StockListWriter` 增加枚举和 `Remove`：读全部字节 → 识别 UTF-8 BOM 先写入输出 → 按 `\n` 逐行（去掉行尾 `\r`）用 ASCII 匹配 → 命中则跳过该行及其换行，否则原样复制 → 命中任意行才写回。

- [ ] **Step 4: 运行构建与自测**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: 新增 6 项 PASS，共 98 项，`ALL PASS` + `BUILD OK`。

- [ ] **Step 5: 检查点**

报告结果。不要提交。

---

### Task 8: 右键移出与文档

**Files:**
- Modify: `src/RowView.cs`
- Modify: `src/WidgetForm.cs`
- Modify: `README.md`

**Interfaces:**
- Consumes: `StockListWriter.Remove`、`StockListRemoveResult`（Task 7）。
- RowView：`name.Tag = code; price.Tag = code; pct.Tag = code;`
- WidgetForm 新增字段：`ToolStripMenuItem _removeItem`、`ToolStripSeparator _removeSeparator`、`string _contextCode`。
- WidgetForm 新增方法：`void OnContextMouseDown(object sender, MouseEventArgs e)`、`void OnMenuOpening(object sender, CancelEventArgs e)`、`void OnRemoveClick(object sender, EventArgs e)`；`using System.ComponentModel;`。

**WidgetForm 改动：**

- 菜单顺序：`移出列表` / 分隔线 / `搜索股票…` / 分隔线 / `退出`；保存移出项与其分隔线的字段。
- `_menu.Opening += OnMenuOpening`：`_contextCode` 非空时显示两项，否则隐藏两项。
- 构造函数和 `ConfigureRowTarget` 都挂 `OnContextMouseDown`；右键时 `_contextCode = (sender as Control).Tag as string`。
- `OnRemoveClick`：取 `_contextCode` 并清空，`StockList.ToSymbol(code)` 后调用 `StockListWriter.Remove(_configPath, symbol)`；不弹提示，等热更新。
- 菜单打开时若在行上，可显示「移出列表」；空白/提示/状态点 Tag 为空，菜单保持两项。

**README：**

- 功能列表加入：右键股票行 →「移出列表」从当前 stocks.txt 删除该行，保留注释和其他标的。
- 操作列表加入右键移出说明；空白处仍是搜索/退出。
- 已知取舍加入：直接改写、无确认/撤销；整行删除（含别名）；同一标的多行一起删；休市日行隐藏无法移出。
- 自测数量改为 98。

- [ ] **Step 1: 修改 `src/RowView.cs`**

三个行标签设置 `Tag = code`；提示/状态点保持无 Tag。

- [ ] **Step 2: 修改 `src/WidgetForm.cs`**

按 Interfaces 实现菜单、右键目标记录和删除调用。

- [ ] **Step 3: 构建**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `ALL PASS` + `BUILD OK`。

- [ ] **Step 4: GUI 验证**

- 配置两只股票，右键第二行 → 菜单出现「移出列表」且比空白菜单高；点击后文件少一行、挂件少一行。
- 移出最后一只 → 文件无股票行、挂件显示「请编辑 stocks.txt」。
- 右键空提示 → 菜单只有「搜索股票… / 退出」。
- `sh000001` 与 `000001` 同时在配置中，分别移出互不影响。
- 检查 %APPDATA% 文件与注册表无残留副作用（autostart=false）。

- [ ] **Step 5: 更新 README**

按上述文案修改，自测数量核对为 98。

- [ ] **Step 6: 最终构建与检查点**

Run: `powershell -ExecutionPolicy Bypass -File build.ps1`
Expected: `ALL PASS` + `BUILD OK`。报告结果，不要提交。
