# Audit Excel Lineage · 审计 Excel 公式溯源

本地批量提取审计 Excel 的公式、字段含义及跨表引用，在本地脱敏后生成可供外部模型分析的逻辑包。真实业务值、原文件及身份对照表留在使用者电脑。

**当前状态：0.3 版方案与数据契约已准备，提取器和桌面程序尚未实现。** 当前技术路线为 **C# / .NET 10 LTS + WPF + Open XML SDK + SQLite**。

已知实际规模：用户目前最大的 Excel 文件约 **50MB**，已提供 `.xlsx` 结构参考，但不假定该示例就是最大文件，也不据单一样本推断整批格式和布局；办公电脑配置尚未确认。采用流式读取、受限并发、区域依赖和分页展示，并把该文件作为后续本地验收样本；当前没有实际性能测试结果。

## 仓库设置

- 仓库：[frozen-s-e-a/audit-excel-lineage](https://github.com/frozen-s-e-a/audit-excel-lineage)
- 可见性：Private
- 默认分支：`main`
- 产品形态：Windows 本地桌面工具，同时提供命令行入口。
- 仓库内容：代码、文档、完全虚构的测试样例。真实审计底稿不进入 GitHub。

## 阅读顺序

1. [可行实施方案](docs/IMPLEMENTATION_PLAN.md)：目标、架构、范围、算法、存储、界面与验收。
2. [脱敏规范](docs/PRIVACY_SPEC.md)：哪些内容可以导出，如何处理未知信息。
3. [数据契约](docs/DATA_CONTRACT.md)：程序与外部分析模型之间的数据格式。
4. [开发任务](docs/ROADMAP.md)：可以直接交给开发 AI 的任务与验收条件。
5. [50MB 文件与批处理验收](docs/PERFORMANCE_PLAN.md)：本地样本、计量口径、内存与界面响应要求。
6. [技术选型记录](docs/TECHNOLOGY_DECISIONS.md)：选择 C# 的理由和格式适配边界。
7. [仓库与后续开发](docs/REPOSITORY_SETUP.md)：仓库状态、目录约定和后续启动方式。

## 已提供的设计资产

| 文件 | 用途 |
| --- | --- |
| `schemas/logic-package.schema.json` | 小型逻辑包的 JSON Schema 草案，拒绝未定义字段 |
| `examples/synthetic-logic-package.json` | 无真实业务值的虚构示例 |
| `AGENTS.md` | 后续开发时的数据边界与协作约定 |
| `.gitignore` | 排除原文件、运行数据、映射、日志与构建产物 |

Schema 校验仅验证格式，不能证明文本内容已经脱敏；程序必须另外实现本地内容检查。

## 第一版验收目标

选择本地文件夹，批量识别 `.xlsx/.xlsm` 的常见静态公式，建立单元格与区域依赖，使用整批文件一致的匿名编号，展示缺失来源和未支持语法，预览并导出不含真实业务值的逻辑包。`.xls/.xlsb` 通过独立适配器扩展，是否纳入首版在文件格式盘点后确定。

第一版离线运行。模型连接、云端存储、VBA 执行、自动刷新外链均不属于第一版实现范围。

## 计划中的调用方式

下列是待实现的接口约定，当前不能执行：

```powershell
AuditLineage.Cli.exe scan --input "D:\AuditInput" --project "D:\AuditWork\Project01"
AuditLineage.Cli.exe inspect --project "D:\AuditWork\Project01" --target "WB001/S001!J12"
AuditLineage.Cli.exe export --project "D:\AuditWork\Project01" --profile strict --output "D:\AuditExport\Project01"
```

## 设计检查

交付检查：Markdown 本地链接、技术路线一致性、JSON 语法与契约字段、虚构样例引用闭合。具体范围见 [设计交付检查记录](docs/DESIGN_VALIDATION.md)。应用功能、50MB 实际性能和 Windows `.exe` 需要在开发完成后另行验证。

## 0.3 方案更新

按内容来源、值类型和业务含义三个维度分类，公式与缓存分开读取；支持结构识别与业务布局配置分层。真实数据、原始公式和名称映射允许保存在仓库外的本地项目中，对外仍只导出安全 DTO。参考文件只用于发现兼容场景，不记录其实际数据或身份信息到仓库。

详细流程见 [Excel 存储分类与布局适配](docs/EXCEL_CLASSIFICATION.md)。百万级公式、数组结果区域和多布局合成用例加入后续验收，均非已实现能力。
