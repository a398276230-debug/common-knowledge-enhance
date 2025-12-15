// using System;
// using System.Collections.Generic;
// using System.Linq;
// using System.Text;
// using Verse;

// namespace RimTalk.Memory
// {
//     /// <summary>
//     /// ����ע������� v3.3.2.25
//     /// ֱ��ʹ��CommonKnowledgeLibrary���ؼ��ʼ�����
//     /// ? ��ȫ�Ƴ�RAG����
//     /// </summary>
//     public static class SmartInjectionManager
//     {
//         private static int callCount = 0;
        
//         /// <summary>
//         /// ����ע��������
//         /// </summary>
//         public static string InjectSmartContext(
//             Pawn speaker,
//             Pawn listener,
//             string context,
//             int maxMemories = 10,
//             int maxKnowledge = 5)
//         {
//             callCount++;
            
//             if (Prefs.DevMode)
//             {
//                 Log.Message($"[SmartInjection] ?? Call #{callCount}: Speaker={speaker?.LabelShort ?? "null"}, Listener={listener?.LabelShort ?? "null"}");
//             }
            
//             if (speaker == null || string.IsNullOrEmpty(context))
//             {
//                 if (Prefs.DevMode)
//                 {
//                     Log.Warning($"[SmartInjection] ? Null input");
//                 }
//                 return null;
//             }

//             try
//             {
//                 var sb = new StringBuilder();
                
//                 // 1. ע����䣨ʹ��DynamicMemoryInjection��
//                 var memoryComp = speaker.TryGetComp<FourLayerMemoryComp>();
//                 if (memoryComp != null)
//                 {
//                     string memoriesText = DynamicMemoryInjection.InjectMemoriesWithDetails(
//                         memoryComp, 
//                         context, 
//                         maxMemories, 
//                         out var memoryScores
//                     );
                    
//                     if (!string.IsNullOrEmpty(memoriesText))
//                     {
//                         sb.AppendLine("## Character Memories");
//                         sb.AppendLine(memoriesText);
                        
//                         if (Prefs.DevMode)
//                         {
//                             Log.Message($"[SmartInjection]   Injected {memoryScores.Count} memories");
//                         }
//                     }
//                 }
                
//                 // 2. ע�볣ʶ��ֱ��ʹ��CommonKnowledgeLibrary��
//                 var memoryManager = Find.World?.GetComponent<MemoryManager>();
//                 if (memoryManager != null)
//                 {
//                     string knowledgeText = memoryManager.CommonKnowledge.InjectKnowledgeWithDetails(
//                         context,
//                         maxKnowledge,
//                         out var knowledgeScores,
//                         speaker,
//                         listener
//                     );
                    
//                     if (!string.IsNullOrEmpty(knowledgeText))
//                     {
//                         if (sb.Length > 0)
//                             sb.AppendLine();
                        
//                         sb.AppendLine("## World Knowledge");
//                         sb.AppendLine(knowledgeText);
                        
//                         if (Prefs.DevMode)
//                         {
//                             Log.Message($"[SmartInjection]   Injected {knowledgeScores.Count} knowledge entries");
//                         }
//                     }
//                 }
                
//                 string result = sb.ToString();
                
//                 // ? v3.3.2.37: Ӧ����ʾ�ʹ淶������
//                 if (!string.IsNullOrEmpty(result))
//                 {
//                     string normalizedResult = PromptNormalizer.Normalize(result);
                    
//                     if (Prefs.DevMode && normalizedResult != result)
//                     {
//                         Log.Message($"[SmartInjection] ? Applied prompt normalization rules");
//                         Log.Message($"[SmartInjection]   Original: {result.Substring(0, Math.Min(100, result.Length))}...");
//                         Log.Message($"[SmartInjection]   Normalized: {normalizedResult.Substring(0, Math.Min(100, normalizedResult.Length))}...");
//                     }
                    
//                     result = normalizedResult;
//                 }
                
//                 if (Prefs.DevMode)
//                 {
//                     Log.Message($"[SmartInjection] ? Success: {result.Length} chars formatted");
//                 }
                
//                 return string.IsNullOrEmpty(result) ? null : result;
//             }
//             catch (Exception ex)
//             {
//                 Log.Error($"[SmartInjection] ? Exception: {ex.Message}\n{ex.StackTrace}");
//                 return null;
//             }
//         }
        
//         public static int GetCallCount() => callCount;
//     }
// }
