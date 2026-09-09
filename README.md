# 牵着你的宠物 Continued

原作者：乐织终。Continued 维护：Nanaloveyuki。原作者于 2026-09-09 许可续作维护并要求保留署名，见 NOTICE。

## 兼容范围

- 保留原版玩法、Def、`LeadYourPet` 程序集与命名空间、存档类型、Scribe 字段及枚举值。
- 修复鼠灾原版 / Continued 的可选联动：包名检测、生成方法重载与可选参数、访客敌对入口。
- 鼠灾 Continued 需安装同时更新的版本，才能主动识别本模组的新包名。
- 新包名：`nanaloveyuki.leadyourpet.continued`。不包含原版创意工坊发布 ID。

## 替换原版

1. 备份旧存档，退出游戏。
2. 禁用原版，只启用 Continued，保持其他模组与加载顺序不变。
3. 用存档副本检查牵引、锚定、鼠蛋归属、母婴心情、交易和访客；另存后退出并重新读取。

重复启用时，Guard 将原版从启用列表移除并提示重启。本次启动暂不加载续作同名内容，重启后使用续作；不删除原版、不修改存档。模组设置按安装目录存储，换目录后可能需要重新设置绳长等选项。

结构兼容测试不是实机旧档验收。未宣称任意模组组合、任意历史版本存档绝对兼容，也不承诺在进行中的游戏里直接卸载。

## 开发

```powershell
dotnet test Source/Tests/LeadYourPet.Tests.csproj -c Release -p:RimWorldDir="D:/Appdata/Steam/steamapps/common/RimWorld"
./scripts/build-and-deploy.ps1 -BuildOnly
./scripts/verify-guard.ps1
./scripts/verify-guard.ps1 -NoDuplicate
./scripts/verify-integration.ps1 -MouseDisasterAssembly="F:/repo/Ratkin-Great-Famine-Year-Continued/1.6/Assemblies/MouseDisaster.dll"
./scripts/build-and-deploy.ps1
```

测试包含原版规则回归、反射重载/默认参数/out 参数，以及原版持久化文件和 Def 内容快照。部署仅复制运行时文件，校验 SHA-256，不改启用列表、旧版目录或玩家存档。仓库是独立历史，不导入原版 Git 记录。
