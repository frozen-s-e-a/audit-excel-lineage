# 技术选型记录

日期：2026-09-08；方案版本：0.2；状态：采纳为开发路线，尚未做样本性能验证。

## 决策

采用 C#/.NET 10 LTS + WPF，Open XML SDK 读取 .xlsx/.xlsm，SQLite 保存本地索引。CLI 与桌面端复用应用服务。正式应用不依赖 Python；原型阶段的 Python 文档校验脚本不属于应用运行时。

选择依据是 Windows 办公部署、核心与 UI 统一维护、流式处理能力。50MB 文件并不能证明 Python 不可用，也不能证明 C# 一定更快。性能最终由格式、公式规模、读取策略与索引实现决定。

.NET 10 的 LTS 状态依据 [微软支持政策](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)。SDK 与依赖补丁版本在 Windows 验证后锁定，后续定期更新；自包含发布需要随应用维护所带运行时。

## 兼容性分层

| 层面 | 选择与边界 |
| --- | --- |
| 操作系统 | 首个验收目标 Windows 11 x64；WPF 不提供跨平台 UI，旧 Windows/ARM64 单独评估 |
| 文件格式 | Open XML SDK 处理 .xlsx/.xlsm；.xls/.xlsb 通过独立读取适配器评估 |
| 公式语法 | 自建有边界的分词/表达式/引用解析器；SDK 不提供整套依赖追踪 |
| 计算结果 | 不执行重算；查询范围为潜在依赖，不能据此断言真实命中行 |
| 外部来源 | Power Query、透视表、VBA 等先报告存在与盲区，不承诺恢复其全部逻辑 |

## 未选择的主路线

- Python/openpyxl：快速原型仍可行，但此项目正式产品统一采用 .NET。
- 商业读取引擎：若旧格式/二进制格式覆盖收益足够，评估授权、公式原文、外链信息与内存；不预先绑定供应商。
- Excel 自动化：作为后续特殊文件的本地可选通道，不能成为首版安装前提，不自动执行宏或刷新外链。
- Rust/C++：暂未有实测瓶颈足以抵消额外 Excel 生态与维护成本。

## 复议条件

实际 50MB 样本为基础读取器不支持格式、复杂语法占比影响核心审计链路、共享字符串导致内存超预算，或目标电脑不支持选定运行时，均触发范围/适配器调整。先完成本地样本盘点，再确认具体覆盖与交付工期。

参考：[WPF 概览](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/)、[Open XML 流式读取](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/how-to-parse-and-read-a-large-spreadsheet)、[.NET 部署](https://learn.microsoft.com/en-us/dotnet/core/deploying/)。
