# Skill Peek

Skill Peek 是一个 Windows 托盘工具。按全局热键打开技能面板，在工作、规划和写作三页里快速找到需要的技能。每个键显示技能名、分类和一句用途。

![Skill Peek 技能面板](docs/screenshot.png)

## 使用

从 `dist` 双击 `SkillPeek.exe`（首次运行前先跑一次 `build.ps1`）。首次使用的热键是 `Ctrl+Shift+Space`，可在托盘右键菜单里换成 `Alt` + 反引号等预设。

| 操作 | 结果 |
|---|---|
| 全局热键 | 唤起或收起面板 |
| 滚轮、`Tab`、`Shift+Tab` | 正向或反向翻页，也可直接点页签 |
| 方向键、`Home`、`End` | 移动选中项，底部显示完整技能名和用途 |
| 鼠标悬停 | 选中技能 |
| 单击技能或按 `Enter` | 复制 `@skill`，收起面板，并默认粘贴到原窗口 |
| `Esc` | 收起面板 |
| 右上角 **↻** | 重新扫描已安装技能 |

右上角可切换 **按住** 和 **一次**。按住模式在热键松开后收起。托盘菜单可开启开机自启，或把回车改为仅复制。直接粘贴使用 `Ctrl+V`，终端等采用其他粘贴键的应用可以使用仅复制模式。

## 技能与配置

面板保留 72 个固定技能，按常用程度排列。每次唤起都会扫描配置目录的一级子目录。如果扫到技能目录，未匹配的技能会置灰。扫描结果为空时，所有键可用作速查。

配置保存在 `%APPDATA%\SkillPeek\settings.ini`。已有配置会继续生效。

```ini
mode=once
hotkey=Alt+`
paste=1
skills=%USERPROFILE%\.dsh\skills;%USERPROFILE%\.claude\skills
```

要调整技能或顺序，编辑 `ui/skills.tsv` 后重新构建。每行依次填写页签、技能名、分类、用途和分类色，用 Tab 分隔。

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

产物为 `dist/SkillPeek.exe`。构建使用 Windows 自带的 .NET Framework 编译器，并将技能数据嵌入可执行文件。运行时使用原生 WinForms，首次打开时创建面板控件。

## 许可

MIT
