# 股票挂件（StockWidget）

[![build](https://img.shields.io/github/actions/workflow/status/lansel/stock-widget/build.yml?branch=master&label=build)](https://github.com/lansel/stock-widget/actions/workflows/build.yml)

Windows 桌面常驻小挂件，实时显示 A 股/指数涨跌幅。零安装，单个 exe，双击即用。

## 下载

- Gitee：[Releases](https://gitee.com/Ricky_kyli/stock-widget/releases) 下载最新版 `StockWidget.exe` 和 `stocks.txt.sample`
- GitHub：[Releases](https://github.com/lansel/stock-widget/releases)（由 CI 自动构建并挂载）

## 功能

- 实时行情：腾讯行情接口，交易时段 3 秒刷新，红涨绿跌
- 多标的：股票、指数、ETF/LOF 基金均可，一行一个代码，可跟自定义别名（建议 1–5 只）
- 配置即改即生效：支持整行和行内 `#` 注释，保存后自动刷新，无需重启
- 外观自定义：透明度、背景色、名称颜色，可选显示现价列，圆角半透明；名称过长省略，悬停显示全名
- 拖动记忆位置，支持多显示器；拖到屏幕边缘会自动贴回屏内
- 断网/数据陈旧时右上角显示橙色小圆点，正常时不显示
- 右键任意位置 → 退出；单实例运行；开机自启
- 系统 DPI 感知，125%/150% 缩放下文字更清晰
- Win+D：默认显示在桌面最前；可选隐藏模式
- 绿色便携：不写系统目录（仅注册表自启项和 `%APPDATA%\StockWidget\` 位置文件）

## 快速开始

1. 下载 `StockWidget.exe` 和 `stocks.txt.sample`，放到同一文件夹（如 `D:\StockWidget\`）
2. 把 `stocks.txt.sample` 改名为 `stocks.txt`，用记事本编辑股票代码
3. 双击 `StockWidget.exe`，挂件出现在屏幕右上角

### 配置文件

查找顺序：

1. 命令行 `--config <路径>`
2. exe 同目录 `stocks.txt`
3. 桌面 `stocks.txt`

格式示例：

```
# 一行一个代码，可跟自定义显示名（别名）；支持行内 # 注释
600519 茅台
000001
1A0001

# 可选项（不写用默认值）
hideOnWinD=false
autostart=true
showPrice=false
opacity=0.92
bgColor=#202024
nameColor=#F5F5F5
```

`#` 开头的整行和行内 `#` 都会被当作注释；颜色值（如 `bgColor=#202024`）里的 `#` 不会被截断。配置缺失或没有有效代码时，挂件会显示“未找到 stocks.txt”/“请编辑 stocks.txt”提示。

### 配置项

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| 股票代码 | — | 一行一个 6 位数字；支持沪/深/北交所、指数和 ETF/LOF 基金；代码后可跟别名（空格分隔） |
| `hideOnWinD` | `false` | `true` = Win+D 时隐藏挂件，打开任意软件自动恢复 |
| `autostart` | `true` | `false` = 不写开机自启，并移除已有的自启项（热生效） |
| `showPrice` | `false` | `true` = 名称和涨跌幅之间显示现价列 |
| `opacity` | `0.92` | 透明度，0.1（几乎透明）~ 1.0（完全不透明） |
| `bgColor` | `#202024` | 背景色，十六进制 RGB |
| `nameColor` | `#F5F5F5` | 股票名称文字颜色 |

### 常见指数与基金代码

| 指数/基金 | 配置写法 | 说明 |
|------|----------|------|
| 上证指数 | `1A0001` 或 `sh000001` | 注意：`000001` 是平安银行，不是上证指数 |
| 深证成指 | `399001` | |
| 创业板指 | `399006` | |
| 上证50 | `000016` | |
| 沪深300 | `000300` | |
| 中证500 | `000905` | |
| 科创50 | `000688` | |
| 中证1000 | `000852` | |
| 沪深300ETF | `510300` | 基金支持：沪 50/51/56/58 开头，深 15/16 开头 |
| 创业板ETF | `159915` | |

也支持带前缀写法：`sh000300`、`sz399001` 等。

## 操作

- 按住挂件任意位置（名称、百分比、空提示）拖动，位置自动记忆
- 右键任意位置 → 退出
- 右上角橙色圆点表示最近一次行情拉取失败/数据陈旧；正常时不显示
- Win+D：默认显示在桌面最前；`hideOnWinD=true` 时隐藏，打开任意软件自动恢复

## 调试

```
StockWidget.exe --fetch 600519 000001
```

直接打印行情到终端，不启动挂件。

## 从源码构建

使用 Windows 自带的 .NET Framework csc（Win10/11 默认已安装），无第三方依赖：

```
powershell -ExecutionPolicy Bypass -File build.ps1
```

产物在 `bin\StockWidget.exe`，构建过程会自动运行 48 项自测。

## 卸载

1. 任务管理器结束 `StockWidget.exe`
2. 删除 exe 和 `stocks.txt`
3. 删除注册表键 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `StockWidget`
4. （可选）删除 `%APPDATA%\StockWidget\`

## 已知取舍

- 免费行情接口有秒级延迟
- 节假日：内置 2026 年 A 股休市表（来源：[上交所公告](https://www.sse.com.cn/disclosure/announcement/general/c/c_20251222_10802507.shtml)）；2027 年安排公布后随新版本更新，过期时退回旧行为（假日仍显示”盘中”）
- 内存占用约 30–50MB
- 系统 DPI 感知；把挂件拖到不同 DPI 的显示器时由系统拉伸，完整 Per-Monitor V2 未实现
- 配置清空提示有约 500ms 防抖，避免编辑器保存瞬间闪空

## License

[MIT](LICENSE)
