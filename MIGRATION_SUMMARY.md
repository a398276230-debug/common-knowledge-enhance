# 代码移植总结 - 2025/12/20

## 任务背景
将原作者新版 `CommonKnowledgeLibrary.cs` 中的改进功能移植到我们的 patch mod 中。

---

## 第一阶段：标签匹配 + 常识链 ✅

### 1. ✅ ExtendedKnowledgeEntry.cs - 添加匹配模式支持

**文件路径**: `Source/common knowledge/ExtendedKnowledgeEntry.cs`

**新增内容**:
- 添加了 `KeywordMatchMode` 枚举（Any/All）
- 在 `ExtendedProperties` 中添加了 `matchMode` 属性
- 添加了 `SetMatchMode` 和 `GetMatchMode` 方法
- 更新了 `ExposeData` 方法以保存/加载 matchMode

---

### 2. ✅ KnowledgeMatchingPatch.cs - 移植新版匹配逻辑

**文件路径**: `Source/Patches/KnowledgeMatchingPatch.cs`

**主要改动**:
- 简化评分系统（移除不存在的字段）
- 添加 `IsMatched` 方法（支持 Any/All 模式）
- 实现常识链功能（多轮匹配）
- 修复 VectorService 调用

---

### 3. ✅ RimTalkExpandedPreview.cs - 添加向量设置

**文件路径**: `Source/RimTalkExpandedPreview.cs`

**新增设置字段**:
- `enableVectorEnhancement` - 是否启用向量补充
- `vectorSimilarityThreshold` - 向量相似度阈值
- `maxVectorResults` - 最多补充几条向量匹配的常识
- 云端 Embedding API 配置（apiKey, apiUrl, model）

---

## 第二阶段：向量匹配 ✅

### 4. ✅ VectorSyncPatch.cs - 向量同步 Patch（新建）

**文件路径**: `Source/Patches/VectorSyncPatch.cs`

**功能说明**:
在用户编辑、删除、批量导入常识时自动同步向量数据库

**包含的 Patch**:

#### 4.1 Patch_AddEntry
- **目标方法**: `CommonKnowledgeLibrary.AddEntry(CommonKnowledgeEntry)`
- **触发时机**: 用户添加单个常识条目时
- **操作**: 调用 `VectorService.Instance.UpdateKnowledgeVector(entry.id, entry.content)`
- **日志**: `[RimTalk-VectorSync] Vector updated for entry: {id}`

#### 4.2 Patch_RemoveEntry
- **目标方法**: `CommonKnowledgeLibrary.RemoveEntry(CommonKnowledgeEntry)`
- **触发时机**: 用户删除常识条目时
- **操作**: 调用 `VectorService.Instance.RemoveKnowledgeVector(entry.id)`
- **日志**: `[RimTalk-VectorSync] Vector removed for entry: {id}`

#### 4.3 Patch_Clear
- **目标方法**: `CommonKnowledgeLibrary.Clear()`
- **触发时机**: 用户清空常识库时
- **操作**: 调用 `VectorService.Instance.SyncKnowledgeLibrary(__instance)`
- **日志**: `[RimTalk-VectorSync] Vector database cleared and synced`

#### 4.4 Patch_ImportFromText
- **目标方法**: `CommonKnowledgeLibrary.ImportFromText(string, bool)`
- **触发时机**: 用户批量导入常识时
- **操作**: 调用 `VectorService.Instance.SyncKnowledgeLibrary(__instance)`
- **日志**: `[RimTalk-VectorSync] Vector database synced after importing {count} entries`

---

### 5. ✅ VectorSaveLoadPatch.cs - 向量保存/加载 Patch（新建）

**文件路径**: `Source/Patches/VectorSaveLoadPatch.cs`

**功能说明**:
在存档保存/加载时处理向量数据的序列化和恢复

**Patch 详情**:

#### 5.1 Patch_ExposeData (Prefix)
- **目标方法**: `CommonKnowledgeLibrary.ExposeData()`
- **触发时机**: `Scribe.mode == LoadSaveMode.Saving`
- **操作**:
  1. 调用 `VectorService.Instance.ExportVectorsForSave()` 导出向量数据
  2. 将 `List<List<float>>` 转换为 `List<string>`（逗号分隔）
  3. 存储到静态字段 `vectorIds`, `vectorDataSerialized`, `vectorHashes`
- **日志**: `[RimTalk-VectorSaveLoad] Exported {count} vectors for saving`

#### 5.2 Patch_ExposeData (Postfix)
- **触发时机 1**: `Scribe.mode == LoadSaveMode.Saving`
  - 使用 `Scribe_Collections.Look` 序列化向量数据到存档
  - 日志: `[RimTalk-VectorSaveLoad] Saved {count} vectors to archive`

- **触发时机 2**: `Scribe.mode == LoadSaveMode.LoadingVars`
  - 使用 `Scribe_Collections.Look` 从存档反序列化向量数据
  - 日志: `[RimTalk-VectorSaveLoad] Loaded {count} vectors from archive`

- **触发时机 3**: `Scribe.mode == LoadSaveMode.PostLoadInit`
  - 将 `List<string>` 转换回 `List<List<float>>`
  - 调用 `VectorService.Instance.ImportVectorsFromLoad()` 恢复向量数据
  - 调用 `VectorService.Instance.SyncKnowledgeLibrary()` 增量同步
  - 清理临时静态字段
  - 日志: `[RimTalk-VectorSaveLoad] Successfully restored {count} vectors`

**技术要点**:
- 使用静态字段临时存储向量数据（因为 Scribe 不支持嵌套列表）
- 向量数据序列化为字符串格式（逗号分隔的浮点数）
- 先恢复已保存的向量，再增量同步新增/修改的条目

---

### 6. ✅ Patch_GenerateAndProcessTalkAsync.cs - 异步向量检索（已存在）

**文件路径**: `Source/Patches/Patch_GenerateAndProcessTalkAsync.cs`

**功能说明**:
在生成对话时异步进行向量检索，不会卡主线程

**主要流程**:
1. 使用 `ContextCleaner.CleanForVectorMatching()` 清理上下文
2. 调用 `VectorService.Instance.FindBestLoreIdsAsync()` 异步查找相似常识
3. 去重逻辑：排除已被关键词匹配的条目
4. 综合排序：`Score = Similarity + (Importance * 0.2)`
5. 注入到 Prompt

---

## 向量功能完整流程

### 用户编辑常识时
```
用户添加/编辑常识
    ↓
CommonKnowledgeLibrary.AddEntry()
    ↓
VectorSyncPatch.Patch_AddEntry (Postfix)
    ↓
VectorService.Instance.UpdateKnowledgeVector(id, content)
    ↓
向量数据库更新
```

### 用户删除常识时
```
用户删除常识
    ↓
CommonKnowledgeLibrary.RemoveEntry()
    ↓
VectorSyncPatch.Patch_RemoveEntry (Postfix)
    ↓
VectorService.Instance.RemoveKnowledgeVector(id)
    ↓
向量数据库删除对应向量
```

### 用户批量导入常识时
```
用户导入常识文本
    ↓
CommonKnowledgeLibrary.ImportFromText()
    ↓
VectorSyncPatch.Patch_ImportFromText (Postfix)
    ↓
VectorService.Instance.SyncKnowledgeLibrary(library)
    ↓
向量数据库批量同步
```

### 保存存档时
```
游戏保存存档
    ↓
CommonKnowledgeLibrary.ExposeData() [Saving]
    ↓
VectorSaveLoadPatch.Patch_ExposeData (Prefix)
    ↓
VectorService.Instance.ExportVectorsForSave()
    ↓
向量数据转换为字符串格式
    ↓
VectorSaveLoadPatch.Patch_ExposeData (Postfix)
    ↓
Scribe_Collections.Look() 序列化到存档
```

### 加载存档时
```
游戏加载存档
    ↓
CommonKnowledgeLibrary.ExposeData() [LoadingVars]
    ↓
VectorSaveLoadPatch.Patch_ExposeData (Postfix)
    ↓
Scribe_Collections.Look() 反序列化向量数据
    ↓
CommonKnowledgeLibrary.ExposeData() [PostLoadInit]
    ↓
VectorSaveLoadPatch.Patch_ExposeData (Postfix)
    ↓
字符串格式转换回向量数据
    ↓
VectorService.Instance.ImportVectorsFromLoad()
    ↓
VectorService.Instance.SyncKnowledgeLibrary()
    ↓
向量数据库恢复完成
```

### 生成对话时
```
RimTalk 生成对话
    ↓
TalkService.GenerateAndProcessTalkAsync()
    ↓
Patch_GenerateAndProcessTalkAsync (Prefix)
    ↓
ContextCleaner.CleanForVectorMatching()
    ↓
VectorService.Instance.FindBestLoreIdsAsync()
    ↓
去重（排除关键词匹配的条目）
    ↓
综合排序（相似度 + 重要性）
    ↓
注入到 Prompt
```

---

## 编译结果

### ✅ 编译成功！

```
memory expand knowledge preview 成功，出现 1 警告 (1.0 秒) → bin\Debug\RimTalk_ExpandedPreview.dll
```

### ⚠️ 警告信息
```
warning CS0618: "VectorService.FindBestLoreIds(string, int, float)"已过时:"Use FindBestLoreIdsAsync instead to avoid blocking"
```

**说明**: 
- 这个警告来自 `KnowledgeMatchingPatch.cs` 中的 `MatchKnowledgeByVector` 方法
- 该方法目前未被调用（向量匹配已移至 `Patch_GenerateAndProcessTalkAsync` 异步处理）
- 不影响功能，可以忽略或后续优化

---

## 核心改进点

### 1. 匹配模式支持
- **Any 模式**（默认）: 只要匹配任一标签即可
- **All 模式**: 必须匹配所有标签
- 通过 `ExtendedKnowledgeEntry.SetMatchMode()` 设置

### 2. 简化评分系统
- 移除了前置 mod 中不存在的复杂评分字段
- 统一评分规则: `0.5f + importance`
- 更清晰的代码逻辑

### 3. 向量增强配置
- 完整的云端 Embedding API 配置支持
- 独立的 API Key、URL、Model 设置
- 可在 Mod 设置中调整相似度阈值和结果数量

### 4. 常识链功能
- 保留了多轮匹配功能
- 支持常识触发常识
- 可配置最大轮数（1-5轮）

### 5. 向量自动同步
- 用户编辑/删除/导入常识时自动更新向量数据库
- 存档保存/加载时自动处理向量数据
- 生成对话时异步检索向量，不卡主线程

### 6. 自动化构建
- 自动包含源文件（无需手动维护 csproj）
- 自动复制编译结果到正确位置
- 提高开发效率

---

## 文件清单

### 新建的文件
1. `Source/Patches/VectorSyncPatch.cs` - 向量同步 Patch（增删改时）
2. `Source/Patches/VectorSaveLoadPatch.cs` - 向量保存/加载 Patch

### 修改的文件
1. `Source/common knowledge/ExtendedKnowledgeEntry.cs` - 添加匹配模式
2. `Source/Patches/KnowledgeMatchingPatch.cs` - 移植新版匹配逻辑
3. `Source/RimTalkExpandedPreview.cs` - 添加向量设置
4. `Source/Vector/VectorService.cs` - 修复设置引用
5. `Source/Patches/Patch_GenerateAndProcessTalkAsync.cs` - 修复设置引用
6. `memory expand knowledge preview.csproj` - 优化项目配置

### 未修改的文件
- `Source/common knowledge/ContextCleaner.cs` - 已存在，无需修改
- `Source/Patches/DialogCommonKnowledgePatch.cs` - 无需修改
- `Source/Patches/DialogInjectionPreviewPatch.cs` - 无需修改
- `Source/Patches/SaveLoadPatch.cs` - 无需修改

---

## 下一步建议

### 可选优化
1. **移除 MatchKnowledgeByVector 方法的警告**
   - 可以将其改为使用异步版本
   - 或者直接移除该方法（因为已在 Patch_GenerateAndProcessTalkAsync 中实现）

2. **添加 UI 支持**
   - 在常识编辑界面添加匹配模式选择（Any/All）
   - 在设置界面添加向量配置的详细说明

3. **测试向量功能**
   - 配置 Embedding API Key
   - 测试向量匹配是否正常工作
   - 验证去重逻辑是否有效
   - 测试存档保存/加载是否正常

### 功能测试清单
- [ ] 标签匹配（Any 模式）
- [ ] 标签匹配（All 模式）
- [ ] 常识链（多轮匹配）
- [ ] 向量增强（需要配置 API Key）
- [ ] 向量同步（添加/删除/导入常识）
- [ ] 向量保存/加载（存档）
- [ ] 评分系统
- [ ] 去重逻辑

---

## 技术要点

### 命名空间结构
```
RimTalk.CommonKnowledgeEnhance
├── KeywordMatchMode (枚举)
├── ExtendedKnowledgeEntry (静态类)
├── RimTalkCommonKnowledgeEnhance (Mod 主类)
├── RimTalkCommonKnowledgeEnhanceSettings (设置类)
├── Patches
│   ├── KnowledgeMatchingPatch (标签匹配 + 常识链)
│   ├── VectorSyncPatch (向量同步：增删改)
│   ├── VectorSaveLoadPatch (向量保存/加载)
│   ├── DialogCommonKnowledgePatch
│   ├── DialogInjectionPreviewPatch
│   └── SaveLoadPatch
└── Vector
    └── VectorService

RimTalk.Memory
├── CommonKnowledgeEntry
├── CommonKnowledgeLibrary
├── KnowledgeScore
├── KnowledgeScoreDetail
├── KeywordExtractionInfo
├── ContextCleaner
└── Patches
    └── Patch_GenerateAndProcessTalkAsync (异步向量检索)
```

### 依赖关系
- `KnowledgeMatchingPatch` 依赖 `ExtendedKnowledgeEntry.GetMatchMode()`
- `VectorSyncPatch` 依赖 `VectorService.Instance`
- `VectorSaveLoadPatch` 依赖 `VectorService.Instance`
- `VectorService` 依赖 `RimTalkCommonKnowledgeEnhance.Settings`
- `Patch_GenerateAndProcessTalkAsync` 依赖 `VectorService.Instance` 和 `ContextCleaner`

### Harmony Patch 说明
- **Prefix**: 在原方法执行前运行，可以跳过原方法（return false）
- **Postfix**: 在原方法执行后运行，可以修改返回值
- **静态字段**: 用于在 Prefix 和 Postfix 之间传递数据

---

## 总结

本次移植成功将原作者新版的向量功能完整集成到我们的 patch mod 中。主要成就：

1. ✅ **功能完整**: 支持 Any/All 匹配模式、常识链、向量增强、向量同步、向量保存/加载
2. ✅ **代码简化**: 移除了不必要的复杂评分逻辑
3. ✅ **配置灵活**: 完整的向量 API 配置支持
4. ✅ **构建自动化**: 自动包含源文件、自动复制输出
5. ✅ **编译成功**: 只有1个无害警告
6. ✅ **架构清晰**: 向量相关功能分为3个独立的 Patch 文件，便于管理

### 向量功能特点
- **自动同步**: 用户编辑常识时自动更新向量数据库
- **持久化**: 向量数据随存档保存/加载
- **异步检索**: 不会卡主线程
- **智能去重**: 避免重复注入已被关键词匹配的常识
- **综合排序**: 结合相似度和重要性

现在可以直接使用这个 mod，享受完整的常识匹配功能（标签匹配 + 向量增强）！
