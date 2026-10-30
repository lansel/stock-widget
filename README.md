# 股票挂件（StockWidget）

Windows 桌面常驻小挂件，显示 1–3 只 A 股的名称和实时涨跌幅。零安装，单个 exe。

## 使用

1. 把 `stocks.txt.sample` 复制到桌面，改名为 `stocks.txt`，编辑股票代码
2. 双击 `bin\StockWidget.exe` 运行（首次运行自动写入开机自启）

### 配置文件

- 默认读取桌面 `stocks.txt`
- 也可放在 exe 同目录（优先于桌面）
- 或用命令行指定：`StockWidget.exe --config E:\path\stocks.txt`

格式示例：

```
# 一行一个 6 位股票代码
600519
000001
300750

# Win+D 时隐藏挂件（打开软件自动恢复）；false 或不写则 Win+D 时显示在最前
hideOnWinD=false
```

改完保存即生效，无需重启。

### 调试

```
StockWidget.exe --fetch 600519 000001
```

直接打印行情到终端，不启动挂件。

## 操作

- 按住挂件拖动改变位置（位置自动记忆）
- Win+D：默认显示在桌面最前；`hideOnWinD=true` 时隐藏

## 卸载

1. 结束进程（任务管理器）
2. 删除 `bin\` 目录
3. 删除注册表键：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 下的 `StockWidget`
4. （可选）删除 `%APPDATA%\StockWidget\` 和桌面 `stocks.txt`

## 已知取舍

- 免费行情接口有秒级延迟
- 无节假日日历：法定假日落在工作日时显示"盘中"但数据静止
- 内存占用约 30–50MB
