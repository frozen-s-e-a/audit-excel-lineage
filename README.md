# Audit Excel Lineage · Excel 数值处理

**当前交付：0.4 Windows 基础测试版源码。** 首版按用户要求只处理工作表数值，保留公式和文本，输出另存的 Excel。完整公式溯源、语义识别、参数化和安全 JSON 包仍是后续方案。

## 直接运行

打开 [Windows 构建](https://github.com/frozen-s-e-a/audit-excel-lineage/actions/workflows/windows-build.yml)，选择最新成功运行，在 Artifacts 下载 **AuditLineage-Windows-x64**。完整解压后双击 **AuditLineage.exe**。目标 Windows 11 x64，自带 .NET 运行时，无需安装 Excel 或开发环境。构建成功前不会有下载产物。

添加 .xlsx/.xlsm，选择输出文件夹，点击“开始处理并另存”。支持批量、进度、取消和逐文件错误状态。输入保持只读，不覆盖旧输出。详见 [运行与测试说明](docs/QUICK_START.md)。

## 数值处理规则

| 内容 | 操作 |
| --- | --- |
| 工作表存储为数值的单元格 | 替换为 0，包括数值型日期和百分比 |
| 公式的数值结果缓存 | 删除缓存，公式表达式和属性保留 |
| 公式内部数字、字符串 | 完全保留，不解析或脱敏 |
| 普通文本、数字文本、布尔、错误、ISO 日期文本 | 保留 |
| 名称、元数据、图表/透视/外链缓存、嵌入对象和宏 | 保留 |

**这是仅数值处理工具，不是完整匿名化工具。** Excel 打开后可能重新计算。输出中的公式硬编码、文本及附属对象仍可能含真实数据，不应仅因处理完成就视为适合对外发送。规则详见 [当前脱敏规范](docs/PRIVACY_SPEC.md)。

## 源码与验证

C#/.NET 10 + WPF。核心使用 .NET 自带 ZIP/XML 流式读写，无第三方运行库，不需要数据库。测试从零生成虚构文件，不使用任何真实底稿。

安装 .NET 10 SDK 后双击 run-windows.cmd，或运行：

```powershell
 dotnet run --project src/AuditLineage.Desktop -c Release
 dotnet run --project tests/AuditLineage.Tests -c Release
```

双击 build-windows.cmd 可生成自包含 Windows 程序。CI 执行合成回归、Windows 发布及启动冒烟检查；原生 Excel/WPS 打开和真实大文件性能不由这些检查代替。

## 后续设计文档

[实施方案](docs/IMPLEMENTATION_PLAN.md)、[开发任务](docs/ROADMAP.md)、[性能验收](docs/PERFORMANCE_PLAN.md)、[Excel 分类](docs/EXCEL_CLASSIFICATION.md)、[逻辑包契约](docs/DATA_CONTRACT.md)保留为后续设计。其公式参数化、SQLite、CLI 和安全 DTO 路径不属于 0.4 基础程序，不能据文档声称已经实现。

实际数据可保存在你的本地项目目录；源码仓库和 CI 仅含代码、文档及合成测试。当前构建包未做代码签名。
