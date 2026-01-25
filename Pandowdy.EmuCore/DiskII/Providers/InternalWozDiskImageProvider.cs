using System.Diagnostics;
using Pandowdy.EmuCore.Interfaces;

namespace Pandowdy.EmuCore.DiskII.Providers;

/// <summary>
/// Native implementation of WOZ disk image provider that reads WOZ format directly.
/// </summary>
/// <remarks>
/// <para>
/// This implementation reads and interprets WOZ files without external dependencies,
/// providing full control over track data access and quarter-track handling.
/// </para>
/// <para>
/// <strong>WOZ Format Support:</strong><br/>
/// - WOZ 1.0: Fixed 6656-byte track storage (35 tracks Ã— 4 quarters)
/// - WOZ 2.0: Variable-length track storage in 512-byte blocks
/// </para>
/// <para>
/// <strong>File Structure:</strong><br/>
/// - Header (12 bytes): Signature + CRC + metadata
/// - INFO chunk (+$14, 60 bytes): Disk metadata
/// - TMAP chunk (+$58, 160 bytes): Track map (quarter-track â†’ track index)
/// - TRKS chunk (+$100): Actual bit stream data per track
/// </para>
/// <para>
/// <strong>Timing:</strong> Uses 45/11 cycles per bit (â‰ˆ4.090909) for cycle-accurate
/// disk rotation at 250 kHz bit rate with 1.023 MHz CPU.
/// </para>
/// </remarks>
public class InternalWozDiskImageProvider : IDiskImageProvider, IDisposable
{
    private readonly Stream _stream;
    private readonly string _filePath;
    private bool _disposed;

    // WOZ file structure
    private byte _wozVersion;          // 1 or 2
    private uint _fileCrc32;           // CRC32 of file (excluding first 12 bytes)
    private bool _isWriteProtected;
    private bool _isSynchronized;      // From INFO chunk
    private byte _bootSectorFormat;    // From INFO chunk
    private byte _optimalBitTiming = 32;   // From INFO chunk (default 32 = 4Î¼s per bit)

    // Track data structures
    private readonly byte[] _trackMap = new byte[160];      // TMAP: quarter-track â†’ track index
    private readonly byte[][] _trackData = new byte[160][]; // Track bit stream data
    private readonly int[] _trackBitCount = new int[160];   // Bits per track

    private int _currentQuarterTrack;
    private readonly HashSet<int> _missingTracks = [];

    // Random bit pattern for simulating MC3470 behavior when no track data exists
    // Approximately 30% ones, matching the TypeScript reference implementation
    private static readonly byte[] RandBits =
        [0, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1];
    private int _randPos;

    private const int MaxTracks = 35;
    private const int QuartersPerTrack = 4;
    private const int MaxQuarterTracks = 160;

    /// <summary>
    /// Gets the file path of the disk image.
    /// </summary>
    public string FilePath => _filePath;

    /// <summary>
    /// Gets whether this image supports write operations.
    /// </summary>
    public bool IsWritable => _stream.CanWrite && !_isWriteProtected;

    /// <summary>
    /// Gets or sets whether the disk is write-protected.
    /// </summary>
    public bool IsWriteProtected
    {
        get => _isWriteProtected;
        set => _isWriteProtected = value;
    }

    /// <summary>
    /// Gets the current quarter-track position.
    /// </summary>
    public int CurrentQuarterTrack => _currentQuarterTrack;

    /// <summary>
    /// Initializes a new instance of the <see cref="InternalWozDiskImageProvider"/> class.
    /// </summary>
    /// <param name="filePath">Full path to the .woz disk image file.</param>
    /// <exception cref="ArgumentNullException">Thrown if filePath is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file doesn't exist.</exception>
    /// <exception cref="InvalidDataException">Thrown if the file is not a valid .woz format.</exception>
    public InternalWozDiskImageProvider(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Disk image file not found: {filePath}", filePath);
        }

        _filePath = filePath;
        // Use ReadWrite sharing for concurrent test access
        _stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        try
        {
            ParseWozFile();
            Debug.WriteLine($"Loaded internal .woz disk image: {filePath} (WOZ v{_wozVersion}, {DiskIIConstants.CyclesPerBit:F6} cycles/bit)");
        }
        catch
        {
            _stream?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Parses the WOZ file header and chunks.
    /// </summary>
    private void ParseWozFile()
    {
        _stream.Seek(0, SeekOrigin.Begin);

        // Read entire file for CRC validation
        byte[] fileData = new byte[_stream.Length];
        _stream.Read(fileData, 0, fileData.Length);
        _stream.Seek(0, SeekOrigin.Begin);

        // Read 12-byte header
        Span<byte> header = fileData.AsSpan(0, 12);

        // Check signature: "WOZ1" or "WOZ2" (0xFF 0x0A 0x0D 0x0A)
        if (header[0] != 'W' || header[1] != 'O' || header[2] != 'Z')
        {
            throw new InvalidDataException($"Invalid WOZ signature: {header[0]:X2} {header[1]:X2} {header[2]:X2}");
        }

        _wozVersion = (byte)(header[3] - '0');
        if (_wozVersion != 1 && _wozVersion != 2)
        {
            throw new InvalidDataException($"Unsupported WOZ version: {_wozVersion}");
        }

        // Verify the 0xFF 0x0A 0x0D 0x0A marker
        if (header[4] != 0xFF || header[5] != 0x0A || header[6] != 0x0D || header[7] != 0x0A)
        {
            throw new InvalidDataException("Invalid WOZ header marker bytes");
        }

        // Read stored CRC32 (little-endian)
        _fileCrc32 = BitConverter.ToUInt32(header.Slice(8, 4));

        // Validate CRC32 if present (non-zero)
        if (_fileCrc32 != 0)
        {
            uint actualCrc = CalculateCrc32(fileData, 12); // Start after 12-byte header
            if (_fileCrc32 != actualCrc)
            {
                throw new InvalidDataException(
                    $"CRC checksum error in {_filePath}\n" +
                    $"Stored CRC: 0x{_fileCrc32:X8}, Calculated CRC: 0x{actualCrc:X8}");
            }
            Debug.WriteLine($"  CRC32 validated: 0x{_fileCrc32:X8}");
        }
        else
        {
            Debug.WriteLine("  CRC32 not present (0x00000000) - skipping validation");
        }

        // Parse chunks
        ParseInfoChunk();
        ParseTmapChunk();
        ParseTrksChunk();
    }

    /// <summary>
    /// Calculates CRC32 checksum for WOZ file validation.
    /// </summary>
    /// <remarks>
    /// Uses the standard CRC32 polynomial (0xEDB88320) matching the TypeScript implementation.
    /// </remarks>
    private static uint CalculateCrc32(byte[] data, int startOffset)
    {
        const uint Polynomial = 0xEDB88320;
        uint crc = 0xFFFFFFFF;

        for (int i = startOffset; i < data.Length; i++)
        {
            crc ^= data[i];
            for (int j = 0; j < 8; j++)
            {
                if ((crc & 1) != 0)
                {
                    crc = (crc >> 1) ^ Polynomial;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }

        return ~crc;
    }

    /// <summary>
    /// Parses the INFO chunk at offset +$14 (60 bytes).
    /// </summary>
    private void ParseInfoChunk()
    {
        _stream.Seek(0x0C, SeekOrigin.Begin); // Skip to chunk header

        // Read chunk header (8 bytes)
        Span<byte> chunkHeader = stackalloc byte[8];
        _stream.Read(chunkHeader);

        // Verify "INFO" chunk
        if (chunkHeader[0] != 'I' || chunkHeader[1] != 'N' || chunkHeader[2] != 'F' || chunkHeader[3] != 'O')
        {
            throw new InvalidDataException("Expected INFO chunk at offset 0x0C");
        }

        uint chunkSize = BitConverter.ToUInt32(chunkHeader.Slice(4, 4));
        if (chunkSize < 60)
        {
            throw new InvalidDataException($"INFO chunk too small: {chunkSize} bytes");
        }

        // Read INFO data (at least 60 bytes)
        Span<byte> infoData = stackalloc byte[60];
        _stream.Read(infoData);

        // Byte 0: WOZ version (1 or 2)
        byte infoVersion = infoData[0];
        if (infoVersion != _wozVersion)
        {
            Debug.WriteLine($"Warning: INFO version ({infoVersion}) doesn't match header version ({_wozVersion})");
        }

        // Byte 1: Disk type (1 = 5.25", 2 = 3.5")
        byte diskType = infoData[1];
        if (diskType != 1)
        {
            throw new InvalidDataException($"Only 5.25\" disks supported (found type {diskType})");
        }

        // Byte 2: Write protected (0 = no, 1 = yes)
        _isWriteProtected = infoData[2] != 0;

        // Byte 3: Synchronized (0 = no, 1 = yes)
        _isSynchronized = infoData[3] != 0;

        // Byte 4: Cleaned (0 = no, 1 = yes) - not used
        // Byte 5-36: Creator (32 bytes, space-padded string) - not used

        // Byte 39: Boot sector format
        _bootSectorFormat = infoData[39];

        // Byte 40: Optimal bit timing (4Î¼s = 32)
        _optimalBitTiming = infoData[40];
        if (_optimalBitTiming == 0)
        {
            _optimalBitTiming = 32; // Default
        }

        Debug.WriteLine($"  INFO: WOZ v{_wozVersion}, 5.25\", WriteProtect={_isWriteProtected}, Sync={_isSynchronized}, OptimalTiming={_optimalBitTiming}");
    }

    /// <summary>
    /// Parses the TMAP chunk at offset +$58 (160 bytes).
    /// </summary>
    private void ParseTmapChunk()
    {
        _stream.Seek(0x50, SeekOrigin.Begin); // Skip to chunk header

        // Read chunk header
        Span<byte> chunkHeader = stackalloc byte[8];
        _stream.Read(chunkHeader);

        // Verify "TMAP" chunk
        if (chunkHeader[0] != 'T' || chunkHeader[1] != 'M' || chunkHeader[2] != 'A' || chunkHeader[3] != 'P')
        {
            throw new InvalidDataException("Expected TMAP chunk at offset 0x50");
        }

        uint chunkSize = BitConverter.ToUInt32(chunkHeader.Slice(4, 4));
        if (chunkSize != 160)
        {
            throw new InvalidDataException($"TMAP chunk should be 160 bytes (found {chunkSize})");
        }

        // Read track map (160 bytes) - maps quarter-track index to track storage index
        // 0xFF = no track data for this quarter-track
        _stream.Read(_trackMap, 0, 160);

        int trackCount = _trackMap.Count(b => b != 0xFF);
        Debug.WriteLine($"  TMAP: {trackCount} tracks mapped");
    }

    /// <summary>
    /// Parses the TRKS chunk starting at offset +$100.
    /// </summary>
    private void ParseTrksChunk()
    {
        _stream.Seek(0xF8, SeekOrigin.Begin); // Skip to chunk header

        // Read chunk header
        Span<byte> chunkHeader = stackalloc byte[8];
        _stream.Read(chunkHeader);

        // Verify "TRKS" chunk
        if (chunkHeader[0] != 'T' || chunkHeader[1] != 'R' || chunkHeader[2] != 'K' || chunkHeader[3] != 'S')
        {
            throw new InvalidDataException("Expected TRKS chunk at offset 0xF8");
        }

        uint chunkSize = BitConverter.ToUInt32(chunkHeader.Slice(4, 4));
        Debug.WriteLine($"  TRKS: chunk size = {chunkSize} bytes");

        // Track data starts at +$100
        if (_wozVersion == 1)
        {
            ParseTrksV1();
        }
        else
        {
            ParseTrksV2();
        }
    }

    /// <summary>
    /// Parses WOZ 1.0 TRKS data (fixed 6656-byte blocks per track).
    /// </summary>
    private void ParseTrksV1()
    {
        const int TrackBlockSize = 6656;
        _stream.Seek(0x100, SeekOrigin.Begin);

        // WOZ 1: Each track gets 6656 bytes, regardless of actual data size
        // Bytes 0-6645: Track data
        // Bytes 6646-6647: Number of bytes used (little-endian uint16)
        // Bytes 6648-6649: Number of bits used (little-endian uint16)
        // Bytes 6650-6651: Splice point (little-endian uint16)
        // Bytes 6652-6653: Splice nibble (uint8) + splice bit count (uint8)
        // Bytes 6654-6655: Reserved

        for (int trkIdx = 0; trkIdx < 160; trkIdx++)
        {
            long trackOffset = 0x100 + (trkIdx * TrackBlockSize);
            _stream.Seek(trackOffset, SeekOrigin.Begin);

            // Read track metadata from end of block
            byte[] trackBlock = new byte[TrackBlockSize];
            _stream.Read(trackBlock, 0, TrackBlockSize);

            ushort bytesUsed = BitConverter.ToUInt16(trackBlock, 6646);
            ushort bitsUsed = BitConverter.ToUInt16(trackBlock, 6648);

            if (bitsUsed > 0)
            {
                // Copy actual track data
                _trackData[trkIdx] = new byte[bytesUsed];
                Array.Copy(trackBlock, 0, _trackData[trkIdx], 0, bytesUsed);
                _trackBitCount[trkIdx] = bitsUsed;

                Debug.WriteLine($"    Track {trkIdx}: {bitsUsed} bits ({bytesUsed} bytes)");
            }
            else
            {
                _trackData[trkIdx] = [];
                _trackBitCount[trkIdx] = 0;
            }
        }
    }

    /// <summary>
    /// Parses WOZ 2.0 TRKS data (variable-length, 512-byte blocks).
    /// </summary>
    private void ParseTrksV2()
    {
        // WOZ 2: Track descriptors (8 bytes each) at +$100, data starts at +$600
        _stream.Seek(0x100, SeekOrigin.Begin);

        Span<byte> trackDescriptor = stackalloc byte[8];

        for (int trkIdx = 0; trkIdx < 160; trkIdx++)
        {
            _stream.Read(trackDescriptor);

            ushort startingBlock = BitConverter.ToUInt16(trackDescriptor[..2]);
            ushort blockCount = BitConverter.ToUInt16(trackDescriptor.Slice(2, 2));
            uint bitCount = BitConverter.ToUInt32(trackDescriptor.Slice(4, 4));

            if (startingBlock == 0 && blockCount == 0)
            {
                // No data for this track
                _trackData[trkIdx] = [];
                _trackBitCount[trkIdx] = 0;
                continue;
            }

            // Read track data from blocks
            long dataOffset = startingBlock * 512L;
            int dataSize = blockCount * 512;

            byte[] trackData = new byte[dataSize];
            long savedPos = _stream.Position;
            _stream.Seek(dataOffset, SeekOrigin.Begin);
            _stream.Read(trackData, 0, dataSize);
            _stream.Seek(savedPos, SeekOrigin.Begin);

            _trackData[trkIdx] = trackData;
            _trackBitCount[trkIdx] = (int)bitCount;

            Debug.WriteLine($"    Track {trkIdx}: {bitCount} bits (block {startingBlock}, {blockCount} blocks)");
        }
    }

    /// <summary>
    /// Generates a random bit to simulate MC3470 controller behavior when no track data exists.
    /// </summary>
    private bool RandBit()
    {
        _randPos++;
        return RandBits[_randPos & 0x1F] == 1;
    }

    /// <summary>
    /// Sets the current quarter-track position.
    /// </summary>
    public void SetQuarterTrack(int qTrack)
    {
        if (_currentQuarterTrack != qTrack)
        {
            int printableTrack = qTrack / QuartersPerTrack;
            int printableQuarter = (qTrack % QuartersPerTrack) * 25;
            _currentQuarterTrack = qTrack;
            Debug.WriteLine($"InternalWozDiskImageProvider: Disk head moved to track {printableTrack}.{printableQuarter} (quarter-track {qTrack})");
        }
    }

    /// <summary>
    /// Reads the next bit from the current track.
    /// </summary>
    public bool? GetBit(ulong cycleCount)
    {
        // Clamp to valid range
        if (_currentQuarterTrack < 0 || _currentQuarterTrack >= MaxQuarterTracks)
        {
            return RandBit();
        }

        // Check if we've already determined this track is missing
        if (_missingTracks.Contains(_currentQuarterTrack))
        {
            return RandBit();
        }

        // Get track index from map
        byte trackIndex = _trackMap[_currentQuarterTrack];

        if (trackIndex == 0xFF)
        {
            // No track data mapped - log once and mark as missing
            Debug.WriteLine($"InternalWozDiskImageProvider: Quarter-track {_currentQuarterTrack} not mapped in TMAP");
            _missingTracks.Add(_currentQuarterTrack);
            return RandBit();
        }

        // Get track data
        byte[] trackData = _trackData[trackIndex];
        int bitCount = _trackBitCount[trackIndex];

        if (trackData.Length == 0 || bitCount == 0)
        {
            // Track mapped but has no data - return random noise
            if (!_missingTracks.Contains(_currentQuarterTrack))
            {
                Debug.WriteLine($"InternalWozDiskImageProvider: Track {trackIndex} (qtrack {_currentQuarterTrack}) has no data");
                _missingTracks.Add(_currentQuarterTrack);
            }
            return RandBit();
        }

        // Calculate bit position based on cycle count (continuously spinning disk)
        int bitPosition = (int)((cycleCount / DiskIIConstants.CyclesPerBit) % bitCount);

        // Extract bit from track data
        int byteIndex = bitPosition / 8;
        int bitInByte = 7 - (bitPosition % 8); // MSB first

        if (byteIndex >= trackData.Length)
        {
            return RandBit(); // Safety check
        }

        byte dataByte = trackData[byteIndex];
        bool bit = ((dataByte >> bitInByte) & 1) == 1;

        return bit;
    }

    /// <summary>
    /// Writes a bit to the current track.
    /// </summary>
    public bool WriteBit(bool bit, ulong cycleCount)
    {
        if (_isWriteProtected || !IsWritable)
        {
            return false;
        }

        // Clamp to valid range
        if (_currentQuarterTrack < 0 || _currentQuarterTrack >= MaxQuarterTracks)
        {
            return false;
        }

        // Get track index from map
        byte trackIndex = _trackMap[_currentQuarterTrack];

        if (trackIndex == 0xFF)
        {
            return false; // Can't write to unmapped track
        }

        // Get track data
        byte[] trackData = _trackData[trackIndex];
        int bitCount = _trackBitCount[trackIndex];

        if (trackData.Length == 0 || bitCount == 0)
        {
            return false; // Can't write to empty track
        }

        // Calculate bit position
        int bitPosition = (int)((cycleCount / DiskIIConstants.CyclesPerBit) % bitCount);

        // Write bit to track data
        int byteIndex = bitPosition / 8;
        int bitInByte = 7 - (bitPosition % 8); // MSB first

        if (byteIndex >= trackData.Length)
        {
            return false; // Safety check
        }

        if (bit)
        {
            trackData[byteIndex] |= (byte)(1 << bitInByte);
        }
        else
        {
            trackData[byteIndex] &= (byte)~(1 << bitInByte);
        }

        return true;
    }

    /// <summary>
    /// Flushes any pending writes to the disk image file.
    /// </summary>
    public void Flush()
    {
        if (_isWriteProtected || !IsWritable)
        {
            return;
        }

        // TODO: Write modified track data back to file
        // For now, changes only exist in memory
        _stream.Flush();
        Debug.WriteLine($"InternalWozDiskImageProvider: Flushed changes to {_filePath}");
    }

    /// <summary>
    /// Disposes resources used by this provider.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Flush();
            _stream?.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
