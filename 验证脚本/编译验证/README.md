# C# 编译自查工具（可选）

用本机安装的 Unity 2022.3 真实程序集对全部游戏脚本做一次编译级检查，
用于**改代码后、打开 Unity 前**快速抓出编译错误（CS0103/CS1061/CS0246 等），
避免在 Unity 里进入 SAFE MODE 来回试错。

## 使用方法

```powershell
dotnet build "D:\wenjian\rougelike\验证脚本\编译验证\CompileCheck.csproj" -v q --nologo
```

期望输出：`0 个警告 / 0 个错误`。

## 前置条件

- 本机装有 .NET SDK（`dotnet --version` 可用）。
- `CompileCheck.csproj` 中的 HintPath 指向 Unity 安装目录
  （当前为 `D:\unity\unity editor\2022.3.20f1c1\`）。
  若换了机器或 Unity 版本，请用本机路径替换，或先跑下面命令找到引擎 DLL 位置：

  ```powershell
  Get-ChildItem "D:\unity" -Recurse -Filter "UnityEngine.CoreModule.dll"
  ```

## 说明

- 引用范围：UnityEngine.CoreModule / UIModule / Physics2DModule / TextRenderingModule /
  AudioModule / JSONSerializeModule / InputLegacyModule / InputModule + UnityEngine.UI（ugui）。
- 该检查覆盖本游戏用到的全部 API；Unity 编辑器内编译结果应与之一致。
- 这不是 Unity 编辑器内的完整导入验证（不覆盖资源管线、预制体、序列化等），
  首次在 Unity 中打开仍建议看一眼 Console。

## 验证记录

- 2026-09-23：修复 ShopUI 缺 Hide()、GameManager 字段名大小写（waveBonusBase）、
  HUDController 缺 using 后，全量编译 0 警告 0 错误。
