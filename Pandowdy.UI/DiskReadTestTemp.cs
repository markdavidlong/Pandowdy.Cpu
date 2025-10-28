using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommonUtil;
using DiskArc;
using DiskArc.Multi;
using static DiskArc.Defs;

namespace pandowdy;

/// <summary>
/// Temporary class for testing disk read functionality.
/// Contains methods extracted from MainWindow for testing purposes.
/// </summary>
public class DiskReadTestTemp
{
    private readonly AppHook mAppHook;
    private readonly Action<string> mAppendText;
    private readonly Window mWindow;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="appHook">Application hook for DiskArc operations.</param>
    /// <param name="appendText">Callback to append text to output.</param>
    /// <param name="window">Reference to the window for file dialogs.</param>
    public DiskReadTestTemp(AppHook appHook, Action<string> appendText, Window window)
    {
        mAppHook = appHook;
        mAppendText = appendText;
        mWindow = window;
    }

    /// <summary>
    /// Shows a file picker dialog to select a disk image or archive file.
    /// </summary>
    /// <param name="defaultStartLocation">Default directory to start in.</param>
    /// <returns>Full path to selected file, or null if cancelled.</returns>
    public async Task<string?> GetDiskFileToProcess(string defaultStartLocation)
    {
        var topLevel = TopLevel.GetTopLevel(mWindow);
        if (topLevel == null)
        {
            mAppendText("Error: Unable to get top level window\n");
            return null;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Select Disk Image or Archive File",
            AllowMultiple = false,
            SuggestedStartLocation = await topLevel.StorageProvider.TryGetFolderFromPathAsync(
                new Uri(defaultStartLocation))
        });

        if (files.Count == 0)
        {
            // User cancelled
            return null;
        }

        return files[0].Path.LocalPath;
    }

    /// <summary>
    /// Handles reading a disk image or archive file.
    /// Opens file picker, analyzes file, and displays contents.
    /// </summary>
    /// <param name="lastDiskPath">Last used directory path (will be updated with selected file's directory)</param>
    /// <returns>Updated last disk path after file selection</returns>
    public async Task<string> HandleDiskRead(string lastDiskPath)
    {
        string? fileName = await GetDiskFileToProcess(lastDiskPath);

        if (fileName == null)
        {
            // User cancelled
            return lastDiskPath;
        }

        // Update the last used path to the directory of the selected file
        string? directory = Path.GetDirectoryName(fileName);
        if (!string.IsNullOrEmpty(directory))
        {
            lastDiskPath = directory;
        }

        try
        {
            // Open the archive file.
            using FileStream arcFile = new(fileName, FileMode.Open,
                FileAccess.ReadWrite);

            // Analyze the file.  Disk images and file archives are handled differently.
            string? ext = Path.GetExtension(fileName);
            FileAnalyzer.AnalysisResult result = FileAnalyzer.Analyze(arcFile, ext,
                mAppHook, out FileKind kind, out SectorOrder orderHint);
            if (result != FileAnalyzer.AnalysisResult.Success)
            {
                mAppendText("Archive or disk image not recognized\n");
                return lastDiskPath;
            }

            if (IsDiskImageFile(kind))
            {
                if (!HandleDiskImage(arcFile, kind, orderHint))
                {
                    mAppendText("Error: !HandleDiskImage\n");
                    return lastDiskPath;
                }
            }
            else
            {
                if (!HandleFileArchive(arcFile, kind))
                {
                    mAppendText("Error: !HandleFileArchive\n");
                    return lastDiskPath;
                }
            }
        }
        catch (IOException ex)
        {
            // Probably a FileNotFoundException.
            mAppendText("Error: " + ex.Message + "\n");
        }

        return lastDiskPath;
    }

    public bool HandleDiskImage(Stream arcFile, FileKind kind, SectorOrder orderHint)
    {
        using IDiskImage? diskImage = FileAnalyzer.PrepareDiskImage(arcFile, kind, mAppHook);
        if (diskImage == null)
        {
            mAppendText("Unable to prepare disk image\n");
            return false;
        }

        // Analyze the contents of the disk to determine file order and filesystem.
        if (!diskImage.AnalyzeDisk(null, orderHint, IDiskImage.AnalysisDepth.Full))
        {
            mAppendText("Failed to analyze disk image\n");
            return false;
        }
        if (diskImage.Contents is IFileSystem)
        {
            PrintFileSystemContents((IFileSystem) diskImage.Contents);
            return true;
        }
        else if (diskImage.Contents is IMultiPart)
        {
            return HandleMultiPart((IMultiPart) diskImage.Contents);
        }
        else
        {
            mAppendText("ARRRGH!\n");     // this shouldn't be possible
            return false;
        }
    }

    public bool HandleMultiPart(IMultiPart partitions)
    {
        // We could dive into each one, but for this simple example we'll just list them.
        mAppendText("Found multi-part image with " + partitions.Count + " partitions:\n");
        StringBuilder sb = new StringBuilder();
        foreach (Partition part in partitions)
        {
            sb.Clear();
            sb.AppendFormat("  start={0,-9} count={1,-9}",
                part.StartOffset / BLOCK_SIZE, part.Length / BLOCK_SIZE);
            APM_Partition? apmPart = part as APM_Partition;
            if (apmPart != null)
            {
                sb.AppendFormat(" name='{0}' type='{1}'",
                    apmPart.PartitionName, apmPart.PartitionType);
            }
            mAppendText(sb.ToString() + "\n");
        }
        return true;
    }

    public void PrintFileSystemContents(IFileSystem fs)
    {
        fs.PrepareFileAccess(true);
        IFileEntry volDir = fs.GetVolDirEntry();
        PrintDirectory(volDir);
    }

    public void PrintDirectory(IFileEntry dirEntry)
    {
        foreach (IFileEntry entry in dirEntry)
        {
            mAppendText(entry.FullPathName + "\n");

            if (entry.IsDirectory)
            {
                PrintDirectory(entry);
            }
        }
    }

    public bool HandleFileArchive(Stream arcFile, FileKind kind)
    {
        using IArchive? archive = FileAnalyzer.PrepareArchive(arcFile, kind, mAppHook);
        if (archive == null)
        {
            mAppendText("Unable to open file archive\n");
            return false;
        }

        foreach (IFileEntry entry in archive)
        {
            mAppendText(entry.FullPathName + "\n");
        }
        return true;
    }
}
