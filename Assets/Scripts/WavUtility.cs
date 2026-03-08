using UnityEngine;

/// <summary>
/// 将 Unity AudioClip 转为 WAV 字节（16-bit PCM），供 Whisper 等 API 使用
/// </summary>
public static class WavUtility
{
    public static byte[] FromAudioClip(AudioClip clip)
    {
        if (clip == null) return null;
        int samples = clip.samples * clip.channels;
        float[] data = new float[samples];
        clip.GetData(data, 0);

        int sampleRate = clip.frequency;
        int numChannels = clip.channels;
        int subChunk2Size = samples * 2;
        int chunkSize = 36 + subChunk2Size;

        using (var ms = new System.IO.MemoryStream())
        {
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(chunkSize);
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)numChannels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * numChannels * 2);
                writer.Write((short)(numChannels * 2));
                writer.Write((short)16);
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(subChunk2Size);
                for (int i = 0; i < samples; i++)
                {
                    short s = (short)(Mathf.Clamp(data[i], -1f, 1f) * 32767f);
                    writer.Write(s);
                }
            }
            return ms.ToArray();
        }
    }
}
