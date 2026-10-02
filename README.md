# 牵着你的宠物 Continued

原作者：乐织终。Continued 维护：Nanaloveyuki。原作者于 2026-09-09 许可续作维护并要求保留署名，见 NOTICE。

## 兼容范围

- 保留原版玩法、Def、`LeadYourPet` 程序集与命名空间、存档类型、Scribe 字段及枚举值。源码按 Core、Leash、MouseEgg、Interaction、Patches、Compat 分目录，类型全名仍是 `LeadYourPet.*`。
- 修复鼠灾原版 / Continued 的可选联动：包名检测、生成方法重载与可选参数、访客敌对入口。
- 鼠灾 Continued 需安装同时更新的版本，才能主动识别本模组的新包名。
- 新包名：`nanaloveyuki.leadyourpet.continued`。`About/PublishedFileId.txt` 是本续作的创意工坊文件 ID，不是原版 ID。
- IrisMenus（`Nanaloveyuki.IrisMenus`，1.6）可选。运行时检测到已启用且支持 1.6，并且公开菜单 API 存在时，用反射登记牵引、幼年鼠族、年龄、来客、离图、主人喂食六页，写入同一套设置。主程序集不引用 IrisMenus.dll。未安装、未启用、不是 1.6 或 API 对不上时不登记，原版设置窗口照常可用。不写入 `modDependencies`。
- 饥与祸通过 `LeadYourPet.LeadYourPetApi` 调用。本模组不检测饥与祸，也不引用它的程序集。

## 替换原版

1. 备份旧存档，退出游戏。
2. 禁用原版，只启用 Continued，保持其他模组与加载顺序不变。
3. 用存档副本检查牵引、锚定、鼠蛋归属、母婴心情、交易和访客；另存后退出并重新读取。

重复启用时，Guard 将原版从启用列表移除并提示重启。本次启动暂不加载续作同名内容，重启后使用续作；不删除原版、不修改存档。模组设置按安装目录存储，换目录后可能需要重新设置绳长等选项。

结构兼容测试不是实机旧档验收。未宣称任意模组组合、任意历史版本存档绝对兼容，也不承诺在进行中的游戏里直接卸载。

## 主人喂食设置

IrisMenus 下新增「主人喂食」SubItem，列出所有已加载、提供营养且能用于喂食的食物。默认勾选原版及官方扩展来源，模组食物逐项启用；设置按食物名称保存，后来加入的模组食物仍默认关闭。药物、尸体和食物分配器不属于可喂食物品。

可搜索食物名称、内部名称、模组名称和包名，并按 Mod、A–Z（内部名称首字母）、原版/模组来源分类。IrisMenus 全局搜索也能定位到具体食物；没有 IrisMenus 时，从原版模组设置的「主人喂食」按钮打开同一套列表。

手动喂食、自动喂食和主人进食时的分享均遵守该列表；库存优先，地图选食在允许的食物中选择，不会因更优先的食物被禁用而漏掉其余食物。饮食政策、禁用物品、腐败和可达性等原有选食限制保留。

验证：Release 构建零警告、零错误；针对生产程序集的 10 项 headless 检查通过，覆盖默认来源、逐项覆盖、新模组默认禁用、药物拒绝、搜索、A–Z 分类和实际 IrisMenus DLL 的公开 API 绑定。未运行 Unity 菜单画面或实机存档验收。

## 可牵引年龄

IrisMenus 的「年龄」页及原版模组设置新增「不限制牵引年龄」。开启后，动物和智人均忽略年龄上下限，成年囚犯也可使用现有宠物牵引流程；宠物开关、距离、可达性和其他牵引条件仍有效。关闭后恢复原有的生物年龄范围，下限包含、上限不包含，默认 0 至未满 14 岁。也可直接提高上限，例如 40 表示未满 40 岁。该开关不改变角色实际年龄，原有范围值继续保存。

验证：Release 构建零警告、零错误；实际生产方法的 6 项 headless 检查通过，使用 30 岁智人囚犯覆盖默认拒绝、提高上限、上限边界、取消年龄限制、宠物开关仍生效及恢复下限。未运行游戏内牵引动画或实机存档验收。

## 开发

```powershell
dotnet test Source/Tests/LeadYourPet.Tests.csproj -c Release -p:RimWorldDir="D:/Appdata/Steam/steamapps/common/RimWorld"
./scripts/build-and-deploy.ps1 -BuildOnly
./scripts/verify-guard.ps1
./scripts/verify-guard.ps1 -NoDuplicate
./scripts/verify-integration.ps1 -MouseDisasterAssembly="F:/repo/Ratkin-Great-Famine-Year-Continued/1.6/Assemblies/MouseDisaster.dll"
./scripts/deploy.sh
./scripts/build-and-deploy.ps1 -RimWorldDir="E:/Apps/Steam/steamapps/common/RimWorld"
```

测试包含原版规则回归、反射重载/默认参数/out 参数，以及原版持久化文件和 Def 内容快照。部署仅复制运行时文件，校验 SHA-256，不改启用列表、旧版目录或玩家存档。仓库是独立历史，不导入原版 Git 记录。
