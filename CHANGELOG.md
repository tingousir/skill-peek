# 变更记录

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，版本号用 [语义化版本](https://semver.org/lang/zh-CN/)。

## [0.1.1] - 2026-11-21

### 新增
- exe 带版本信息（右键属性可见版本号、产品名）
- Release 附带 SHA-256 校验文件，README 写明校验方法
- README 增加徽章、直达下载、SmartScreen 说明

### 变更
- 从技能清单里移除 `claude-api` 和 `claude-mem` 两条（共 72 条）

## [0.1.0] - 2026-11-21

### 新增
- 全局热键唤起技能面板，三页签（工作 / 规划 / 写作）滚轮翻页
- 按使用频率排序，越常用越靠左上角
- 每次唤起扫描本地 skill 目录，未安装的置灰
- 方向键选择，回车复制 `@skill` 并粘回原窗口
- 按住 / 一次 两种唤起模式
- 托盘菜单：换热键、开机自启、只复制不粘贴、重新扫描
- 单文件 exe，无运行时依赖，仅需系统自带 .NET Framework 4.x
