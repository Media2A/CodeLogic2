using System.Text;
using K4os.Compression.LZ4.Streams;
using Newtonsoft.Json;

namespace CodeLogic.Caching;

/// <summary>
/// Internal compression utilities for cache operations
/// </summary>
internal static class CompressionHelper
{
    /// <summary>
    /// Compresses data using LZ4 algorithm
    /// </summary>
    public static byte[] Compress<T>(T data, K4os.Compression.LZ4.LZ4Level level = K4os.Compression.LZ4.LZ4Level.L00_FAST)
    {
        byte[] bytes = ConvertToBytes(data);

        using var outputStream = new MemoryStream();
        using (var lz4Stream = LZ4Stream.Encode(outputStream, level))
        {
            lz4Stream.Write(bytes, 0, bytes.Length);
        }

        return outputStream.ToArray();
    }

    /// <summary>
    /// Decompresses LZ4-compressed data
    /// </summary>
    public static T? Decompress<T>(byte[] compressedData)
    {
        if (compressedData == null || compressedData.Length == 0)
            return default;

        using var inputStream = new MemoryStream(compressedData);
        using var lz4Stream = LZ4Stream.Decode(inputStream);
        using var outputStream = new MemoryStream();

        lz4Stream.CopyTo(outputStream);
        byte[] decompressedBytes = outputStream.ToArray();

        return ConvertFromBytes<T>(decompressedBytes);
    }

    private static byte[] ConvertToBytes<T>(T obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        return obj switch
        {
            string str => Encoding.UTF8.GetBytes(str),
            byte[] bytes => bytes,
            _ => Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj))
        };
    }

    private static T? ConvertFromBytes<T>(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return default;

        var targetType = typeof(T);

        if (targetType == typeof(string))
        {
            return (T)(object)Encoding.UTF8.GetString(bytes);
        }

        if (targetType == typeof(byte[]))
        {
            return (T)(object)bytes;
        }

        // Try to deserialize as JSON for other types
        string json = Encoding.UTF8.GetString(bytes);
        return JsonConvert.DeserializeObject<T>(json);
    }
}
