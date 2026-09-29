# DeepSeek 构筑建议：Unity 直连测试

现在的 Unity 客户端只认游戏自身的武器改造 ID。DeepSeek 不会生成代码、攻击效果或数值，也不会自动购买。当前目录只有“土豆剑：磨锋直刺”一项，因此必须先持有土豆剑，才有可推荐的候选。

## 当前测试模式：直接调用

`GameSettings.asset` 的 `Weapon Forge Api Key` 已配置测试 Key。进入商店后，按钮会优先从 Unity 直连 DeepSeek，不需要启动 Python 代理，也不需要输入提示词。点击“AI 推荐构筑”后，游戏把当前角色、持有武器、道具、波次和允许的改造候选发给模型；最多两条建议占用现有三个商店位，仍须玩家主动购买。每次点击都会发起真实 API 请求，可能计费。

测试完成后，清空 `Weapon Forge Api Key`，并在 DeepSeek 平台撤销或更换这把测试 Key。打包分发的客户端资源可被提取；这条直连路径只适用于你已接受风险的临时测试，不能保护 Key。远程分享版本应改用 HTTPS 后端代理。

## 可选：本机代理模式

1. 在项目根目录打开 PowerShell，运行 `python UnityProject\Tools\deepseek_forge_proxy.py`。如 `python` 命令不可用，改用本机 Python 可执行文件的完整路径。
2. 在终端出现 `DeepSeek API Key（输入不回显）` 时输入开放平台的 API Key，按回车。不要把 Key 写进 Unity、截图、聊天或提交到项目。也可事先通过 `DEEPSEEK_API_KEY` 环境变量提供，但不要把明文写在共享脚本里。
3. Unity Inspector 打开 `Assets/_Game/Resources/Config/GameSettings.asset`，先清空 `Weapon Forge Api Key`，再将 `Weapon Forge Endpoint` 填为 `http://127.0.0.1:8765/forge`。本机回环 HTTP 是唯一允许的明文地址；远程服务必须 HTTPS。
4. 点击 Play 并进入商店，点“AI 推荐构筑”。它会占用未锁定商店位，仍需玩家购买。
5. 测完把 Endpoint 清空，按钮会恢复为明确标注的“本地改造测试”。终端按 Ctrl+C 可关闭代理。

每次点 AI 按钮会真正请求 DeepSeek API，可能计费。没有 Key 或断网时，普通商店和下一波仍可用。请求超时默认 25 秒，可在同一 SO 修改。

## 已验证与未验证

- 本地契约测试：`python -B UnityProject\Tools\test_deepseek_forge_proxy.py`，使用假的 DeepSeek 响应，不联网、不计费，覆盖合法 ID、目录外 ID、截断响应和重复候选。
- C# 编译检查：`dotnet build 验证脚本/编译验证/CompileCheck.csproj --no-restore`。
- 尚未用真实 Key 调用 DeepSeek，也尚未在 Unity Play 模式实测整段操作。

目前 SO 中有 11 条改造候选，覆盖土豆剑、拳套、手枪、冲锋枪、霰弹枪、狙击枪和加特林。剑气/拳风复用已有投射物图片；弹墙在碰到场地边缘时反射；穿透与伤害、攻速代价均由 `GameContent.asset` 中的改造字段控制。已有改造互斥组保证同一把武器上冲突路线不能叠加。

这个脚本只监听 `127.0.0.1`，**不能直接供朋友的手机连接**。要让朋友使用，需要把同样的服务部署在你控制的 HTTPS 后端，加入访问控制和调用限额，并由服务器保管 Key；不要把 Key 填入 Unity 客户端或打包文件。
