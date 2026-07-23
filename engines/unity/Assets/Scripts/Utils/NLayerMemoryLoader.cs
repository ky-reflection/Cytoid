using System;
using System.IO;
using NLayer;
using UnityEngine;

public sealed class NLayerMemoryLoader : IDisposable
{
    private readonly byte[] bytes;
    private readonly string filename;
    private readonly object fileLock = new object();
    private MpegFile file;

    public NLayerMemoryLoader(byte[] bytes, string filename)
    {
        this.bytes = bytes;
        this.filename = filename;
        file = CreateFile();
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

    private MpegFile CreateFile()
    {
        return new MpegFile(new MemoryStream(bytes, false));
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
