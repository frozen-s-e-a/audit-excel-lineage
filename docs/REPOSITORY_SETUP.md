# 仓库与后续开发

目标仓库：[frozen-s-e-a/audit-excel-lineage](https://github.com/frozen-s-e-a/audit-excel-lineage)，私有，默认分支 main。用户已创建仓库。本轮初始化提交包含 0.2 方案、脱敏规范、契约与虚构示例，应用尚未实现。

## 目录约定

读取 README、IMPLEMENTATION_PLAN、PRIVACY_SPEC、DATA_CONTRACT 与 ROADMAP 后开始开发。src、tests、benchmarks 为待创建目录，当前不提供可执行程序。后续按分层建立 .NET 解决方案；Windows 构建与发布 .exe。

真实输入、SQLite 项目数据、名称映射及导出放在代码仓库之外。仓库只存文档、代码与从零虚构的样例。不要提交本地性能样本或包含真实字段的错误日志。

## 开发顺序

先完成 ROADMAP 的 P01/P02/P04，并验证流式读取可行性；随后完成 CLI 提取、依赖、脱敏、导出闭环，最后实现 WPF 与自包含目录发布。SDK 和 NuGet 依赖在目标 Windows 验证后锁定。

仓库暂不附加开源许可证，许可选择待定。后续功能建议通过分支和 PR 留存验证记录。
