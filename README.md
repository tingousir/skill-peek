# Skill Peek

按一个热键，在任何窗口上唤起一块技能面板：72 个技能分三页，常用的排在左上角，没装的置灰。方向键选，回车把 `@skill` 粘回你刚才的输入框。

![Skill Peek 技能面板](docs/screenshot.png)

## 下载就能用

1. 打开 [Releases](../../releases)，下载最新版里的 **`SkillPeek.exe`**
2. 双击运行

就这两步。**一个 33 KB 的 exe，不用安装、没有依赖、不联网**（系统自带的 .NET Framework 4.x 之外什么都不需要）。

启动后没有窗口，只有托盘图标。默认热键 `Ctrl+Shift+Space`，托盘右键可以换成 `Alt` + 反引号等预设。

> 为什么默认不是 `Ctrl+Space`：中文 Windows 上那个组合归输入法（搜狗、微软拼音都用它切中英文），热键注册会"成功"但按键根本到不了程序。

## 用法

| 操作 | 结果 |
|---|---|
| 全局热键 | 唤起 / 收起 |
| 滚轮、`Tab`、`Shift+Tab` | 三页之间切换，也可直接点页签 |
| 方向键、`Home`、`End` | 移动选中项 |
| `Enter` 或单击 | 复制 `@skill`，收起面板，粘贴回原窗口 |
| `Esc` | 收起 |
| 右上角 **↻** | 重新扫描本地已安装的技能 |

右上角可切换 **按住 / 一次**：按住模式下热键一松就收起。托盘菜单里可以开开机自启，或把回车改成"只复制不粘贴"（终端这类 `Ctrl+V` 不是粘贴的地方用）。

## 自己的技能清单

面板每次唤起都会扫一遍配置目录下的一级子目录，扫到的技能才亮，其余置灰。路径写在配置里：

```ini
; %APPDATA%\SkillPeek\settings.ini
mode=once
hotkey=Ctrl+Shift+Space
paste=1
skills=%USERPROFILE%\.dsh\skills;%USERPROFILE%\.claude\skills
```

要改清单本身（加技能、改顺序、改分类色），编辑 `ui/skills.tsv`，每行五列用 Tab 分隔：

```
工作	tdd	工程	先写会失败的测试	#3E7A96
```

**行的顺序就是显示顺序**，越靠前越常用，也就越靠近左上角。改完重新构建即可。

## 自己构建

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

不需要装 .NET SDK——用 Windows 自带的 `csc.exe` 编译，技能数据作为资源嵌进 exe。产物是 `dist/SkillPeek.exe`，单文件。

## 许可

MIT
