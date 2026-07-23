using System;
using System.IO;
using NLayer;
using UnityEngine;

// Credits: https://github.com/r2123b/Load-Mp3-into-Audioclip
public class NLayerLoader
{
    private readonly string filePath;
    private readonly string filename;
    private readonly object fileLock = new object();
    private MpegFile file;

    public NLayerLoader(string filePath)
    {
        filePath = filePath.Replace("file://", "");
        this.filePath = filePath;
        filename = Path.GetFileNameWithoutExtension(filePath);
        file = new MpegFile(filePath);
    }

    public AudioClip LoadAudioClip()
    {
        return AudioClip.Create(filename,
            (int) (file.Length / sizeof(float) / file.Channels),
             file.Channels,
            file.SampleRate,
            true,
            data =>
            {
                lock (fileLock)
                {
                    if (file == null)
                    {
                        Array.Clear(data, 0, data.Length);
                        return;
                    }

                    file.ReadSamples(data, 0, data.Length);
                }
            },
            position =>
            {
                lock (fileLock)
                {
                    if (file == null) return;
                    file.Time = TimeSpan.FromSeconds(position * 1.0f / file.SampleRate);
                }
            });
    }

    public void Dispose()
    {
        lock (fileLock)
        {
            file?.Dispose();
            file = null;
        }
    }
}
