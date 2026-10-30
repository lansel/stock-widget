# 股票挂件（StockWidget）

Windows 桌面常驻小挂件，实时显示 A 股/指数涨跌幅。零安装，单个 exe（约 24KB），双击即用。

## 下载

从 [Releases](https://gitee.com/Ricky_kyli/stock-widget/releases) 下载最新版 `StockWidget.exe` 和 `stocks.txt.sample`。

## 功能

- 实时行情：腾讯行情接口，交易时段 3 秒刷新，红涨绿跌
- 多标的：股票、指数均可，一行一个代码（建议 1–5 只）
- 配置即改即生效：编辑 `stocks.txt` 保存，几秒内自动刷新，无需重启
- 外观自定义：透明度、背景色、名称颜色，圆角半透明
- 拖动记忆位置、开机自启、单实例运行
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
# 一行一个代码，# 后为注释
600519
000001
1A0001

# 可选项（不写用默认值）
hideOnWinD=false
opacity=0.92
bgColor=#202024
nameColor=#F5F5F5
```

### 配置项

| 配置项 | 默认值 | 说明 |
|--------|--------|------|
| 股票代码 | — | 一行一个 6 位数字；支持沪/深/北交所和指数 |
| `hideOnWinD` | `false` | `true` = Win+D 时隐藏挂件，打开任意软件自动恢复 |
| `opacity` | `0.92` | 透明度，0.1（几乎透明）~ 1.0（完全不透明） |
| `bgColor` | `#202024` | 背景色，十六进制 RGB |
| `nameColor` | `#F5F5F5` | 股票名称文字颜色 |

### 常见指数代码

| 指数 | 配置写法 | 说明 |
|------|----------|------|
| 上证指数 | `1A0001` 或 `sh000001` | 注意：`000001` 是平安银行，不是上证指数 |
| 深证成指 | `399001` | |
| 创业板指 | `399006` | |
| 上证50 | `000016` | |
| 沪深300 | `000300` | |
| 中证500 | `000905` | |
| 科创50 | `000688` | |
| 中证1000 | `000852` | |

也支持带前缀写法：`sh000300`、`sz399001` 等。

## 操作

- 按住挂件拖动改变位置（位置自动记忆）
- Win+D：默认显示在桌面最前；`hideOnWinD=true` 时隐藏

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

产物在 `bin\StockWidget.exe`，构建过程会自动运行 22 项自测。

## 卸载

1. 任务管理器结束 `StockWidget.exe`
2. 删除 exe 和 `stocks.txt`
3. 删除注册表键 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `StockWidget`
4. （可选）删除 `%APPDATA%\StockWidget\`

## 已知取舍

- 免费行情接口有秒级延迟
- 无节假日日历：法定假日落在工作日时显示"盘中"但数据静止
- 内存占用约 30–50MB

## License

[MIT](LICENSE)
