using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace Netimobiledevice.Plist;

/// <summary>
/// Parses, saves, and creates a Plist file
/// </summary>
public static class PropertyList
{
    private static bool IsFormatBinary(Stream stream)
    {
        byte[] buf = new byte[8];
        // Read in first 8 bytes
        stream.ReadExactly(buf);
        // Rewind
        stream.Seek(0, SeekOrigin.Begin);
        return ValidateBinaryHeader(buf);
    }

    private static async Task<bool> IsFormatBinaryAsync(Stream stream)
    {
        byte[] buf = new byte[8];
        // Read in first 8 bytes
        await stream.ReadExactlyAsync(buf);
        // Rewind
        stream.Seek(0, SeekOrigin.Begin);
        return ValidateBinaryHeader(buf);
    }

    private static PropertyNode LoadAsBinary(Stream stream)
    {
        BinaryFormatReader reader = new BinaryFormatReader();
        return reader.Read(stream);
    }

    private static async Task<PropertyNode> LoadAsBinaryAsync(Stream stream)
    {
        BinaryFormatReader reader = new BinaryFormatReader();
        return await reader.ReadAsync(stream).ConfigureAwait(false);
    }

    private static PropertyNode LoadAsXml(Stream stream)
    {
        // Set resolver to null in order to avoid calls to apple.com to resolve DTD
        XmlReaderSettings settings = new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Ignore,
        };

        using (XmlReader reader = XmlReader.Create(stream, settings)) {
            reader.MoveToContent();
            reader.ReadStartElement("plist");

            reader.MoveToContent();
            PropertyNode node = NodeFactory.Create(reader.LocalName);
            node.ReadXml(reader);

            reader.ReadEndElement();

            return node;
        }
    }

    private static async Task<PropertyNode> LoadAsXmlAsync(Stream stream)
    {
        // Set resolver to null in order to avoid calls to apple.com to resolve DTD
        XmlReaderSettings settings = new XmlReaderSettings {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
        };

        using (XmlReader reader = XmlReader.Create(stream, settings)) {
            await reader.MoveToContentAsync();
            reader.ReadStartElement("plist");

            await reader.MoveToContentAsync();
            PropertyNode node = NodeFactory.Create(reader.LocalName);
            await node.ReadXmlAsync(reader);

            reader.ReadEndElement();

            return node;
        }
    }

    private static bool ValidateBinaryHeader(byte[] buf)
    {
        if (Encoding.UTF8.GetString(buf, 0, 6) != "bplist") {
            return false;
        }

        string versionString = Encoding.UTF8.GetString(buf, 6, 2);
        return versionString switch {
            "00" => true,
            _ => throw new NotImplementedException($"The binary plist version {versionString} is not implemented yet"),
        };
    }

    /// <summary>
    /// Saves the Plist to the specified stream.
    /// </summary>
    /// <param name="rootNode">Root node of the Plist structure.</param>
    /// <param name="stream">The stream in which the PList is saves.</param>
    /// <param name="format">The format of the Plist (Binary/Xml).</param>
    internal static void Save(PropertyNode rootNode, Stream stream, PlistFormat format)
    {
        if (format == PlistFormat.Xml) {
            const string newLine = "\n";

            XmlWriterSettings sets = new XmlWriterSettings {
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "\t",
                NewLineChars = newLine,
            };

            using (MemoryStream tmpStream = new MemoryStream()) {
                using (XmlWriter xmlWriter = XmlWriter.Create(tmpStream, sets)) {
                    xmlWriter.WriteStartDocument();
                    xmlWriter.WriteDocType("plist", "-//Apple Computer//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null);

                    // write out nodes, wrapped in plist root element
                    xmlWriter.WriteStartElement("plist");
                    xmlWriter.WriteAttributeString("version", "1.0");
                    rootNode.WriteXml(xmlWriter);
                    xmlWriter.WriteEndElement();
                    xmlWriter.Flush();
                }

                // XmlWriter always inserts a space before element closing (e.g. <true />)
                // whereas the Apple parser can't deal with the space and expects <true/>
                tmpStream.Seek(0, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(tmpStream)) {
                    // Prevent the output of the BOM by creating a UTF8Encoding that doesn't emit the UTF8 identifier at the start
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true)) {
                        writer.NewLine = newLine;
                        for (string? line = reader.ReadLine(); line != null; line = reader.ReadLine()) {
                            if (line.Trim() == "<true />") {
                                line = line.Replace("<true />", "<true/>");
                            }
                            if (line.Trim() == "<false />") {
                                line = line.Replace("<false />", "<false/>");
                            }
                            writer.WriteLine(line);
                        }
                    }
                }
            }
        }
        else {
            BinaryFormatWriter writer = new BinaryFormatWriter();
            writer.Write(stream, rootNode);
        }
    }

    /// <summary>
    /// Saves the Plist to the specified stream.
    /// </summary>
    /// <param name="rootNode">Root node of the Plist structure.</param>
    /// <param name="stream">The stream in which the PList is saves.</param>
    /// <param name="format">The format of the Plist (Binary/Xml).</param>
    internal static async Task SaveAsync(PropertyNode rootNode, Stream stream, PlistFormat format)
    {
        if (format == PlistFormat.Xml) {
            const string newLine = "\n";

            XmlWriterSettings sets = new XmlWriterSettings {
                Async = true,
                Encoding = Encoding.UTF8,
                Indent = true,
                IndentChars = "\t",
                NewLineChars = newLine,
            };

            using (MemoryStream tmpStream = new MemoryStream()) {
                using (XmlWriter xmlWriter = XmlWriter.Create(tmpStream, sets)) {
                    await xmlWriter.WriteStartDocumentAsync().ConfigureAwait(false);
                    await xmlWriter.WriteDocTypeAsync("plist", "-//Apple Computer//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null).ConfigureAwait(false);

                    // write out nodes, wrapped in plist root element
                    await xmlWriter.WriteStartElementAsync(null, "plist", null).ConfigureAwait(false);
                    await xmlWriter.WriteAttributeStringAsync(null, "version", null, "1.0").ConfigureAwait(false);
                    await rootNode.WriteXmlAsync(xmlWriter).ConfigureAwait(false);
                    await xmlWriter.WriteEndElementAsync().ConfigureAwait(false);
                    await xmlWriter.FlushAsync().ConfigureAwait(false);
                }

                // XmlWriter always inserts a space before element closing (e.g. <true />)
                // whereas the Apple parser can't deal with the space and expects <true/>
                tmpStream.Seek(0, SeekOrigin.Begin);
                using (StreamReader reader = new StreamReader(tmpStream)) {
                    // Prevent the output of the BOM by creating a UTF8Encoding that doesn't emit the UTF8 identifier at the start
                    using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true)) {
                        writer.NewLine = newLine;
                        for (string? line = await reader.ReadLineAsync().ConfigureAwait(false); line != null; line = await reader.ReadLineAsync().ConfigureAwait(false)) {
                            if (line.Trim() == "<true />") {
                                line = line.Replace("<true />", "<true/>");
                            }
                            if (line.Trim() == "<false />") {
                                line = line.Replace("<false />", "<false/>");
                            }
                            await writer.WriteLineAsync(line).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
        else {
            BinaryFormatWriter writer = new BinaryFormatWriter();
            await writer.WriteAsync(stream, rootNode).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Loads the Plist from specified stream.
    /// </summary>
    /// <param name="stream">The stream containing the Plist.</param>
    /// <returns>A <see cref="PropertyNode"/> object loaded from the stream</returns>
    public static PropertyNode Load(Stream stream)
    {
        bool isBinary = IsFormatBinary(stream);
        // Detect binary format, and read using the appropriate method
        return isBinary ? LoadAsBinary(stream) : LoadAsXml(stream);
    }

    /// <summary>
    /// Loads the Plist from specified stream.
    /// </summary>
    /// <param name="stream">The stream containing the Plist.</param>
    /// <returns>A <see cref="PropertyNode"/> object loaded from the stream</returns>
    public static async Task<PropertyNode> LoadAsync(Stream stream)
    {
        bool isBinary = await IsFormatBinaryAsync(stream);
        // Detect binary format, and read using the appropriate method
        return isBinary ? await LoadAsBinaryAsync(stream) : await LoadAsXmlAsync(stream);
    }

    /// <summary>
    /// Loads the Plist from the specified byte array
    /// </summary>
    /// <param name="data">The byte array containing the Plist.</param>
    /// <returns>A <see cref="PropertyNode"/> object loaded from the stream</returns>
    public static PropertyNode LoadFromByteArray(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data)) {
            return Load(ms);
        }
    }

    /// <summary>
    /// Loads the Plist from the specified byte array asyncronously
    /// </summary>
    /// <param name="data">The byte array containing the Plist.</param>
    /// <returns>A <see cref="PropertyNode"/> object loaded from the stream</returns>
    public static async Task<PropertyNode> LoadFromByteArrayAsync(byte[] data)
    {
        using (MemoryStream ms = new MemoryStream(data)) {
            return await LoadAsync(ms);
        }
    }

    /// <summary>
    /// Exports the Plist as a byte array.
    /// </summary>
    /// <param name="rootNode">Root node of the Plist structure.</param>
    /// <param name="format">The format of the Plist (Binary/Xml).</param>
    /// <returns>The byte array representation of the plist</returns>
    public static byte[] SaveAsByteArray(PropertyNode rootNode, PlistFormat format)
    {
        using (MemoryStream ms = new MemoryStream()) {
            Save(rootNode, ms, format);
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ToArray();
        }
    }

    /// <summary>
    /// Exports the Plist as a byte array.
    /// </summary>
    /// <param name="rootNode">Root node of the Plist structure.</param>
    /// <param name="format">The format of the Plist (Binary/Xml).</param>
    /// <returns>The byte array representation of the plist</returns>
    public static async Task<byte[]> SaveAsByteArrayAsync(PropertyNode rootNode, PlistFormat format)
    {
        using (MemoryStream ms = new MemoryStream()) {
            await SaveAsync(rootNode, ms, format).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            return ms.ToArray();
        }
    }

    public static string SaveAsString(PropertyNode rootNode, PlistFormat format)
    {
        using (MemoryStream ms = new MemoryStream()) {
            Save(rootNode, ms, format);
            ms.Seek(0, SeekOrigin.Begin);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
    public static async Task<string> SaveAsStringAsync(PropertyNode rootNode, PlistFormat format)
    {
        using (MemoryStream ms = new MemoryStream()) {
            await SaveAsync(rootNode, ms, format).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            return Encoding.UTF8.GetString(ms.ToArray());
        }
    }
}
