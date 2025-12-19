# RimTalk Common Knowledge Enhance - Vector Enhancement

## 📋 概述

本增强模块为 RimTalk Common Knowledge Enhance Mod 添加了基于 ONNX Runtime 的语义向量检索功能，能够在 AI 对话前自动匹配并注入最相关的世界书（Lore）内容。

## 🏗️ 架构说明

### 核心组件

1. **NativeLoader.cs** - 原生库加载器
   - 自动检测操作系统
   - 在 Mod 启动时加载 `onnxruntime.dll`
   - 使用 Win32 API `LoadLibrary` 手动加载原生库

2. **VectorService.cs** - 向量检索引擎
   - 单例模式设计
   - 加载 ONNX 嵌入模型（all-MiniLM-L6-v2-quantized）
   - 提供文本向量化和相似度计算功能
   - 线程安全的推理调用

3. **Patch_GeminiClient.cs** - 异步拦截补丁
   - 使用 Harmony Prefix 拦截 `GeminiClient.GetChatCompletionAsync`
   - 通过 `TaskCompletionSource` 实现真正的异步拦截
   - 不阻塞主线程，保证 UI 流畅性

## 🔧 依赖项

### Managed DLLs
位于 `1.6\Assemblies`:
- Microsoft.ML.OnnxRuntime.dll
- System.Memory.dll
- System.Buffers.dll
- System.Numerics.Vectors.dll
- System.Runtime.CompilerServices.Unsafe.dll

### Native DLL
位于 `1.6\Native\win-x64`:
- onnxruntime.dll (C++ 原生库)

### ONNX Model
位于 `1.6\Resources`:
- all-MiniLM-L6-v2-quantized.onnx (嵌入模型)

## 🚀 工作流程

### 1. 初始化阶段
```
Mod 启动
  ↓
NativeLoader 静态构造函数执行
  ↓
加载 onnxruntime.dll
  ↓
VectorService 单例初始化
  ↓
加载 ONNX 模型
  ↓
预计算世界书向量
```

### 2. 运行时拦截流程
```
用户发送消息
  ↓
Harmony Prefix 拦截 GetChatCompletionAsync
  ↓
创建 TaskCompletionSource<Payload>
  ↓
返回 tcs.Task 给 UI（保持 Loading 状态）
  ↓
Task.Run 启动后台线程
  ├─ 调用 VectorService.FindBestLore()
  ├─ 计算用户消息向量
  ├─ 与预存向量计算余弦相似度
  └─ 找到最佳匹配 Lore
  ↓
LongEventHandler.ExecuteWhenFinished 回到主线程
  ├─ 将 Lore 注入到 messages 参数
  ├─ 调用原版 GetChatCompletionAsync
  ├─ 获取真实 API 响应
  └─ tcs.SetResult(realPayload)
  ↓
UI 收到完整响应并显示
```

## 🔍 关键技术点

### 1. 异步拦截（不阻塞主线程）
```csharp
// 创建 TCS 欺骗 UI
var tcs = new TaskCompletionSource<Payload>();
__result = tcs.Task;

// 后台计算
Task.Run(() => {
    string lore = VectorService.FindBestLore(userMessage);
    
    // 回到主线程
    LongEventHandler.ExecuteWhenFinished(() => {
        // 注入 Lore 并调用原方法
        var realTask = CallOriginalMethod(...);
        realTask.ContinueWith(t => tcs.SetResult(t.Result));
    });
});

return false; // 跳过原方法
```

### 2. 防止递归调用
```csharp
private static readonly ThreadLocal<bool> _isInsidePatch = 
    new ThreadLocal<bool>(() => false);

static bool Prefix(...) {
    if (_isInsidePatch.Value) {
        return true; // 执行原方法
    }
    // ... 拦截逻辑
}

private static Task<Payload> CallOriginalMethod(...) {
    _isInsidePatch.Value = true;
    // 使用反射调用原方法
    var result = originalMethod.Invoke(...);
    result.ContinueWith(_ => _isInsidePatch.Value = false);
    return result;
}
```

### 3. 线程安全的推理
```csharp
private static readonly object _inferenceLock = new object();

private float[] ComputeEmbedding(string text) {
    lock (_inferenceLock) {
        // ONNX 推理代码
        using (var results = _session.Run(inputs)) {
            // ...
        }
    }
}
```

## ⚠️ 已知限制

### 1. Tokenizer 简化
当前使用简化版 Tokenizer（空格分词 + 固定 token ID），实际效果可能不如完整的 WordPiece tokenizer。

**改进方案**：
- 使用预处理好的词汇表（vocab.json）
- 实现完整的 WordPiece 分词算法
- 或使用 HuggingFace Tokenizers 库（需要额外依赖）

### 2. 硬编码路径
当前所有路径都是硬编码的（`D:\steam\...`），不适合分发。

**改进方案**：
- 使用相对路径或 ModContentPack 获取路径
- 添加配置文件支持
- 自动检测 MEKP Mod 位置

### 3. 示例数据
当前只有 3 条示例 Lore，实际使用需要从 CommonKnowledgeLibrary 读取。

**改进方案**：
- 在 VectorService 初始化时读取所有常识条目
- 预计算所有向量并缓存
- 支持动态添加/删除常识时更新向量

## 🛠️ 调试建议

### 查看日志
所有组件都有详细的日志输出，前缀为：
- `[CommonKnowledgeEnhance] NativeLoader:`
- `[CommonKnowledgeEnhance] VectorService:`
- `[CommonKnowledgeEnhance] Patch_GeminiClient:`

### 常见问题

**Q: 提示 DllNotFoundException**
A: 检查 NativeLoader 日志，确认 onnxruntime.dll 是否成功加载。

**Q: 向量计算失败**
A: 检查 ONNX 模型路径是否正确，模型文件是否存在。

**Q: UI 卡死**
A: 检查是否正确使用了 TaskCompletionSource，确保没有在主线程中执行耗时操作。

**Q: 递归调用导致栈溢出**
A: 检查 ThreadLocal 标记是否正确设置和清除。

## 📝 TODO

- [ ] 实现完整的 WordPiece Tokenizer
- [ ] 支持从 CommonKnowledgeLibrary 动态加载常识
- [ ] 添加向量缓存机制
- [ ] 支持配置文件（路径、阈值等）
- [ ] 添加性能监控和统计
- [ ] 支持多语言（中文分词）
- [ ] 优化向量检索算法（使用 FAISS 等）

## 📄 许可证

与 RimTalk Common Knowledge Enhance Mod 相同。

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

---

**最后更新**: 2025/12/17
**版本**: 1.0.0-alpha
