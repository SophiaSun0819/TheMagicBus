using UnityEngine;

/// <summary>
/// NPC角色数据 - 挂载在每个角色方块上
/// </summary>
public class NPCCharacter : MonoBehaviour
{
    [Header("角色信息")]
    public string characterName = "NPC";
    
    [TextArea(3, 6)]
    public string systemPrompt = "你是一个友好的NPC角色，用简短的中文回答玩家的问题。";

    [Header("对话显示")]
    public string lastPlayerMessage = "";
    public string lastNPCResponse = "";

    [Header("视觉反馈")]
    public bool isTalking = false;

    private Renderer rend;
    private Color originalColor;
    private Color talkingColor = new Color(1f, 0.8f, 0.2f);

    void Start()
    {
        rend = GetComponent<Renderer>();
        if (rend != null)
            originalColor = rend.material.color;
    }

    void Update()
    {
        if (rend != null)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            rend.material.color = isTalking
                ? Color.Lerp(originalColor, talkingColor, pulse)
                : originalColor;
        }
    }

    public void SetTalking(bool talking)
    {
        isTalking = talking;
    }

    public void OnResponseReceived(string response)
    {
        lastNPCResponse = response;
        SetTalking(false);
    }
}
