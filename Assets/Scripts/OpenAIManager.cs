using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.IO;

/// <summary>
/// OpenAI API 管理器 - 单例，负责与OpenAI接口通信（Chat + Whisper 语音转文字）
/// </summary>
[System.Serializable]
public class Config
{
    public string openaiKey;
}
public class OpenAIManager : MonoBehaviour
{
    public static OpenAIManager Instance;

    [Header("OpenAI 配置")]
    [Tooltip("在此填入你的 OpenAI API Key")]
    public string apiKey = "";

    [Tooltip("使用的模型")]
    public string model = "gpt-3.5-turbo";

    private const string CHAT_API_URL = "https://api.openai.com/v1/chat/completions";
    private const string WHISPER_API_URL = "https://api.openai.com/v1/audio/transcriptions";

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
        string path = Application.dataPath + "/config.json";
string json = File.ReadAllText(path);
var data = JsonUtility.FromJson<Config>(json);
apiKey = data.openaiKey;
    }

    /// <summary>
    /// 向指定NPC发送消息并获取回复
    /// </summary>
    public void SendMessage(NPCCharacter npc, string userMessage, System.Action<string> onComplete)
    {
        StartCoroutine(SendMessageCoroutine(npc.systemPrompt, userMessage, onComplete));
    }

    private IEnumerator SendMessageCoroutine(string systemPrompt, string userMessage, System.Action<string> onComplete)
    {
        var messages = new List<object>
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userMessage }
        };

        var requestBody = new
        {
            model = model,
            messages = messages,
            max_tokens = 300,
            temperature = 0.8
        };

        string jsonBody = JsonUtility.ToJson(new SerializableRequest(model, messages));
        // 直接构建JSON字符串（JsonUtility对嵌套list支持有限）
        jsonBody = BuildRequestJson(systemPrompt, userMessage);

        using (UnityWebRequest request = new UnityWebRequest(CHAT_API_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string response = ParseResponse(request.downloadHandler.text);
                Debug.Log("response");
                onComplete?.Invoke(response);
            }
            else
            {
                Debug.LogError("[OpenAI] 请求失败: " + request.error + "\n" + request.downloadHandler.text);
                onComplete?.Invoke("抱歉，我现在无法回应。（" + request.responseCode + "）");
            }
        }
    }

    private string BuildRequestJson(string systemPrompt, string userMessage)
    {
        // 转义引号
        systemPrompt = systemPrompt.Replace("\"", "\\\"").Replace("\n", "\\n");
        userMessage = userMessage.Replace("\"", "\\\"").Replace("\n", "\\n");

        return string.Format(
            "{{\"model\":\"{0}\",\"messages\":[{{\"role\":\"system\",\"content\":\"{1}\"}},{{\"role\":\"user\",\"content\":\"{2}\"}}],\"max_tokens\":300,\"temperature\":0.8}}",
            model, systemPrompt, userMessage
        );
    }

    private string ParseResponse(string jsonResponse)
    {
        // 简单解析 choices[0].message.content
        try
        {
            int contentIdx = jsonResponse.IndexOf("\"content\":");
            if (contentIdx < 0) return "（解析失败）";
            
            // 找到第二次出现的 "content"（第一次在 message 对象里）
            // 更稳健：找 choices 之后的 content
            int choicesIdx = jsonResponse.IndexOf("\"choices\"");
            if (choicesIdx < 0) return "（无choices字段）";
            
            int contentAfterChoices = jsonResponse.IndexOf("\"content\":", choicesIdx);
            if (contentAfterChoices < 0) return "（找不到content）";

            int start = jsonResponse.IndexOf("\"", contentAfterChoices + 10) + 1;
            int end = start;
            while (end < jsonResponse.Length)
            {
                if (jsonResponse[end] == '"' && jsonResponse[end - 1] != '\\')
                    break;
                end++;
            }
            string content = jsonResponse.Substring(start, end - start);
            // 反转义
            content = content.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
            return content;
        }
        catch (System.Exception e)
        {
            Debug.LogError("[OpenAI] 解析错误: " + e.Message);
            return "（解析错误）";
        }
    }

    /// <summary>
    /// 使用自定义 systemPrompt 发送一条用户消息并获取回复（用于语音问答等）
    /// </summary>
    public void SendMessageWithPrompt(string systemPrompt, string userMessage, System.Action<string> onComplete)
    {
        StartCoroutine(SendMessageCoroutine(systemPrompt, userMessage, onComplete));
    }

    /// <summary>
    /// 语音转文字：将 WAV 字节上传到 Whisper API，返回识别出的文本
    /// </summary>
    public void TranscribeAudio(byte[] wavBytes, System.Action<string> onComplete)
    {
        StartCoroutine(TranscribeAudioCoroutine(wavBytes, onComplete));
    }

    private IEnumerator TranscribeAudioCoroutine(byte[] wavBytes, System.Action<string> onComplete)
    {
        string boundary = "UnityFormBoundary" + System.Guid.NewGuid().ToString("N").Substring(0, 16);
        List<byte> body = new List<byte>();

        string header = "--" + boundary + "\r\n" +
            "Content-Disposition: form-data; name=\"file\"; filename=\"voice.wav\"\r\n" +
            "Content-Type: audio/wav\r\n\r\n";
        body.AddRange(Encoding.UTF8.GetBytes(header));
        body.AddRange(wavBytes);
        body.AddRange(Encoding.UTF8.GetBytes("\r\n--" + boundary + "\r\n" +
            "Content-Disposition: form-data; name=\"model\"\r\n\r\nwhisper-1\r\n"));
        body.AddRange(Encoding.UTF8.GetBytes("--" + boundary + "\r\n" +
            "Content-Disposition: form-data; name=\"language\"\r\n\r\nen\r\n"));
        body.AddRange(Encoding.UTF8.GetBytes("--" + boundary + "--\r\n"));

        using (UnityWebRequest request = new UnityWebRequest(WHISPER_API_URL, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body.ToArray());
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "multipart/form-data; boundary=" + boundary);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                string rawResponse = request.downloadHandler.text;
                string text = ParseWhisperResponse(rawResponse);
                if (string.IsNullOrEmpty(text))
                    Debug.LogWarning("[Whisper] 请求成功但识别结果为空。原始响应: " + (rawResponse.Length > 200 ? rawResponse.Substring(0, 200) + "..." : rawResponse));
                onComplete?.Invoke(text ?? "");
            }
            else
            {
                Debug.LogError("[Whisper] 请求失败: " + request.error + "\n响应: " + request.downloadHandler.text);
                onComplete?.Invoke("");
            }
        }
    }

    private string ParseWhisperResponse(string json)
    {
        try
        {
            if (string.IsNullOrEmpty(json)) return "";
            if (json.Contains("\"error\""))
            {
                int msgStart = json.IndexOf("\"message\":");
                if (msgStart >= 0)
                {
                    int q = json.IndexOf("\"", msgStart + 10) + 1;
                    int qEnd = q;
                    while (qEnd < json.Length && (json[qEnd] != '"' || json[qEnd - 1] == '\\')) qEnd++;
                    if (qEnd > q)
                        Debug.LogError("[Whisper] API 返回错误: " + json.Substring(q, qEnd - q));
                }
                return "";
            }
            int idx = json.IndexOf("\"text\":");
            if (idx < 0) return "";
            int start = json.IndexOf("\"", idx + 7) + 1;
            int end = start;
            while (end < json.Length && (json[end] != '"' || json[end - 1] == '\\')) end++;
            if (end > start)
                return json.Substring(start, end - start).Replace("\\n", "\n").Replace("\\\"", "\"").Trim();
        }
        catch (System.Exception e) { Debug.LogError("[Whisper] 解析错误: " + e.Message); }
        return "";
    }

    [System.Serializable]
    private class SerializableRequest
    {
        public string model;
        public SerializableRequest(string m, List<object> msgs) { model = m; }
    }
}
