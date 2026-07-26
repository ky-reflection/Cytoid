using System;
using System.Collections.Generic;
using System.IO;
using NLayer;
using UnityEngine;

// Credits: https://github.com/r2123b/Load-Mp3-into-Audioclip
public class NLayerLoader
{
    private readonly string filePath;
    private readonly string filename;
    private MpegFile file;

    private List<MpegFile> createdFiles = new List<MpegFile>();

    public NLayerLoader(string filePath)
    {
        // AudioClipLoader may pass a percent-encoded file:// URI (via GameLaunchVfs.ToFileUri).
        // Strip/unescape so MpegFile opens the real filesystem path.
        if (filePath.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            filePath = GameLaunchVfs.FromFileUri(filePath);
        }
        this.filePath = filePath;
        filename = Path.GetFileNameWithoutExtension(filePath);
        file = new MpegFile(filePath);
        createdFiles.Add(file);
    }

    public AudioClip LoadAudioClip()
    {
        return AudioClip.Create(filename,
            (int) (file.Length / sizeof(float) / file.Channels),
             file.Channels,
            file.SampleRate,
            true,
            data => file.ReadSamples(data, 0, data.Length),
            position =>
            {
                var f = new MpegFile(filePath);
                createdFiles.Add(f);
                f.Time = TimeSpan.FromSeconds(position * 1.0f / f.SampleRate);
                file = f;
            });
    }

    public void Dispose()
    {
        createdFiles.ForEach(it => it.Dispose());
        file = null;
    }

}