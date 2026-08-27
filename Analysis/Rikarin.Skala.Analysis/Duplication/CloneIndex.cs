using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using Rikarin.Skala.Core;
using Rikarin.Skala.Reporting;

namespace Rikarin.Skala.Analysis.Duplication;

/// <summary>
/// The persisted clone index, <c>.skala/cache/clones.idx</c>.
/// </summary>
/// <remarks>
/// docs/plan/09 § "Duplication": "Index is persisted in <c>.skala/cache/clones.idx</c>, keyed by file
/// content hash, so an unchanged file's windows are not re-hashed."
/// <para>
/// ⚠ What is stored is the <b>normalised token stream</b>, not the window hashes, and the difference
/// matters. Step 3 of the algorithm verifies every candidate exactly against the token stream, so the
/// stream has to be in hand either way; an index holding hashes alone could only be trusted, which is
/// the one thing this rule promises never to do. With the stream cached the lexer — the expensive
/// half — is skipped and the windows are re-derived from an integer array, which is the cheap half.
/// </para>
/// <para>
/// ⚠ Corruption is never a failure, exactly as in <see cref="Caching.DiagnosticCache"/>: a bad
/// magic, an old format, a different tool version, a truncated file or a payload whose checksum does
/// not match discards the whole index and the run goes cold. It never degrades to a partial or a
/// wrong answer, because a stale token stream produces clone groups about code that is no longer
/// there and that failure is invisible in the output.
/// </para>
/// <para>
/// ⚠ Entries are pruned to the files of the current run on save. Unlike the per-file diagnostic
/// cache this index is proportional to the whole corpus's token count; keeping deleted files' streams
/// forever would make it grow without a bound anyone ever looks at.
/// </para>
/// </remarks>
internal sealed class CloneIndex {
    /// <summary>'S' 'K' 'C' 'L'.</summary>
    const uint Magic = 0x4C43_4B53;

    /// <summary>⚠ Bump when the normalisation or the layout changes. An old index is discarded, not read.</summary>
    const int FormatVersion = 1;

    readonly string _path;
    readonly ConcurrentDictionary<string, Entry> _loaded = new(StringComparer.Ordinal);
    readonly ConcurrentDictionary<string, Entry> _live = new(StringComparer.Ordinal);
    volatile bool _changed;
    int _hits;

    CloneIndex(string path) => _path = path;

    /// <summary>⚠ Interlocked: the lex pass that calls <see cref="TryGet"/> is parallel.</summary>
    public int Hits => Volatile.Read(ref _hits);

    /// <summary>Opens the index in <paramref name="cacheDirectory"/>, or an empty one if it cannot be read.</summary>
    public static CloneIndex Load(string cacheDirectory) {
        var index = new CloneIndex(Path.Combine(cacheDirectory, "clones.idx"));
        index.Read();
        return index;
    }

    /// <summary>The cached stream for <paramref name="path"/>, if its content still hashes the same.</summary>
    public TokenStream? TryGet(string path, string contentHash) {
        if (_loaded.TryGetValue(path, out var entry)
            && string.Equals(entry.ContentHash, contentHash, StringComparison.Ordinal)) {
            Interlocked.Increment(ref _hits);
            return entry.Tokens;
        }

        _changed = true;
        return null;
    }

    public void Put(string path, string contentHash, TokenStream tokens) =>
        _live[path] = new Entry(path, contentHash, tokens);

    public void Save() {
        // Nothing new, nothing gone: the bytes on disk already say this.
        if (!_changed && _live.Count == _loaded.Count) {
            return;
        }

        try {
            SkalaDirectory.EnsureForFile(_path);

            // ⚠ Ordinal by path, so two runs over the same tree produce byte-identical files. A cache
            // whose bytes move every run is a cache that shows up in every diff and every backup.
            var entries = _live.Values.ToList();
            entries.Sort(static (left, right) => string.CompareOrdinal(left.Path, right.Path));

            var payload = WritePayload(entries);
            var version = Encoding.UTF8.GetBytes(SkalaVersion.Value);
            var header = new byte[20 + version.Length + 16];
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0), Magic);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), FormatVersion);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8), version.Length);
            version.CopyTo(header.AsSpan(12));
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12 + version.Length), entries.Count);
            BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16 + version.Length), payload.Length);
            XxHash128.Hash(payload).CopyTo(header.AsSpan(20 + version.Length));

            using var file = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None);
            file.Write(header);
            file.Write(payload);
        } catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException) {
            // A read-only tree does not fail a check.
        }
    }

    void Read() {
        try {
            if (!File.Exists(_path)) {
                _changed = true;
                return;
            }

            var bytes = File.ReadAllBytes(_path);
            if (bytes.Length < 20 || BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Magic) {
                _changed = true;
                return;
            }

            if (BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(4)) != FormatVersion) {
                _changed = true;
                return;
            }

            var versionLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8));
            if (versionLength is < 0 or > 64 || bytes.Length < 20 + versionLength + 16) {
                _changed = true;
                return;
            }

            var version = Encoding.UTF8.GetString(bytes, 12, versionLength);
            if (!string.Equals(version, SkalaVersion.Value, StringComparison.Ordinal)) {
                // A different build may normalise differently. Its streams are not this build's.
                _changed = true;
                return;
            }

            var count = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(12 + versionLength));
            var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(16 + versionLength));
            var payloadStart = 20 + versionLength + 16;
            if (count < 0 || payloadLength < 0 || payloadStart + payloadLength != bytes.Length) {
                _changed = true;
                return;
            }

            var payload = bytes.AsSpan(payloadStart, payloadLength);
            if (!XxHash128.Hash(payload).AsSpan().SequenceEqual(bytes.AsSpan(payloadStart - 16, 16))) {
                // ⚠ The checksum is the difference between "the file was truncated" and "the file was
                // truncated and the last entry is now a plausible-looking lie".
                _changed = true;
                return;
            }

            var reader = new Cursor(payload);
            for (var i = 0; i < count; i++) {
                var entry = ReadEntry(ref reader);
                _loaded[entry.Path] = entry;
            }

            if (!reader.AtEnd) {
                _loaded.Clear();
                _changed = true;
            }
        } catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or InvalidDataException
            or OutOfMemoryException) {
            _loaded.Clear();
            _changed = true;
        }
    }

    static Entry ReadEntry(ref Cursor cursor) {
        var path = cursor.ReadString();
        var contentHash = cursor.ReadString();
        var count = cursor.ReadVarInt();

        var codes = new ushort[count];
        for (var i = 0; i < count; i++) {
            codes[i] = cursor.ReadUInt16();
        }

        var starts = new int[count];
        var ends = new int[count];
        var previousEnd = 0;
        for (var i = 0; i < count; i++) {
            var start = previousEnd + cursor.ReadVarInt();
            var end = start + cursor.ReadVarInt();
            starts[i] = start;
            ends[i] = end;
            previousEnd = end;
        }

        return new Entry(path, contentHash, TokenStream.FromArrays(codes, starts, ends));
    }

    static byte[] WritePayload(List<Entry> entries) {
        var total = 0;
        foreach (var entry in entries) {
            total += entry.Path.Length * 3 + entry.ContentHash.Length + 32 + entry.Tokens.Count * 6;
        }

        var writer = new Buffer(total);
        foreach (var entry in entries) {
            writer.WriteString(entry.Path);
            writer.WriteString(entry.ContentHash);

            var tokens = entry.Tokens;
            writer.WriteVarInt(tokens.Count);
            for (var i = 0; i < tokens.Count; i++) {
                writer.WriteUInt16(tokens.Codes[i]);
            }

            // ⚠ Deltas, not absolutes: the gap to the previous token and the token's own width are
            // both small, so the varints are one byte each and the index stays a few bytes a token.
            var previousEnd = 0;
            for (var i = 0; i < tokens.Count; i++) {
                writer.WriteVarInt(tokens.Starts[i] - previousEnd);
                writer.WriteVarInt(tokens.Ends[i] - tokens.Starts[i]);
                previousEnd = tokens.Ends[i];
            }
        }

        return writer.ToArray();
    }

    sealed record Entry(string Path, string ContentHash, TokenStream Tokens);

    /// <summary>A bounds-checked read over the payload. Every overrun is one exception the caller catches.</summary>
    ref struct Cursor(ReadOnlySpan<byte> bytes) {
        readonly ReadOnlySpan<byte> _bytes = bytes;
        int _at;

        public readonly bool AtEnd => _at == _bytes.Length;

        public ushort ReadUInt16() {
            Need(2);
            var value = BinaryPrimitives.ReadUInt16LittleEndian(_bytes.Slice(_at, 2));
            _at += 2;
            return value;
        }

        public int ReadVarInt() {
            var value = 0;
            var shift = 0;
            while (true) {
                Need(1);
                var b = _bytes[_at++];
                value |= (b & 0x7F) << shift;
                if ((b & 0x80) == 0) {
                    break;
                }

                shift += 7;
                if (shift > 28) {
                    throw new InvalidDataException("clones.idx: over-long varint");
                }
            }

            if (value < 0) {
                throw new InvalidDataException("clones.idx: negative length");
            }

            return value;
        }

        public string ReadString() {
            var length = ReadVarInt();
            Need(length);
            var value = Encoding.UTF8.GetString(_bytes.Slice(_at, length));
            _at += length;
            return value;
        }

        readonly void Need(int bytes) {
            if (bytes < 0 || _at + bytes > _bytes.Length) {
                throw new InvalidDataException("clones.idx: truncated");
            }
        }
    }

    /// <summary>A growable byte buffer. <see cref="List{T}"/> of bytes is the wrong shape at this volume.</summary>
    sealed class Buffer(int capacity) {
        byte[] _bytes = new byte[Math.Max(64, capacity)];
        int _at;

        public void WriteUInt16(ushort value) {
            Ensure(2);
            BinaryPrimitives.WriteUInt16LittleEndian(_bytes.AsSpan(_at), value);
            _at += 2;
        }

        public void WriteVarInt(int value) {
            Ensure(5);
            var remaining = (uint)value;
            while (remaining >= 0x80) {
                _bytes[_at++] = (byte)(remaining | 0x80);
                remaining >>= 7;
            }

            _bytes[_at++] = (byte)remaining;
        }

        public void WriteString(string value) {
            var length = Encoding.UTF8.GetByteCount(value);
            WriteVarInt(length);
            Ensure(length);
            Encoding.UTF8.GetBytes(value, _bytes.AsSpan(_at));
            _at += length;
        }

        public byte[] ToArray() => _bytes.AsSpan(0, _at).ToArray();

        void Ensure(int bytes) {
            if (_at + bytes <= _bytes.Length) {
                return;
            }

            Array.Resize(ref _bytes, Math.Max(_bytes.Length * 2, _at + bytes));
        }
    }
}

/// <summary>The content hash a <see cref="CloneIndex"/> entry is keyed by.</summary>
internal static class ContentHash {
    /// <summary>
    /// ⚠ Hashes the UTF-16 the file was read as, not a re-encoded copy. The bytes never leave this
    /// process, so the encoding only has to be the same one twice, and not allocating a megabyte per
    /// file matters more over 4 700 of them.
    /// </summary>
    public static string Of(string text) =>
        Convert.ToHexStringLower(XxHash128.Hash(MemoryMarshal.AsBytes(text.AsSpan())));
}
